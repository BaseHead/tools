//------------------------------------------------------------------------
//
// Project     :
// Filename    : messagehandler.cpp
// Created by  : Bodo
// Description :
//
//------------------------------------------------------------------------
#include "messagehandler.h"

#include "base/source/tqueue.h"
#include "pluginterfaces/host/frame/imessage.h"
#include "pluginterfaces/host/ihostclasses.h"

#include "skicomponent.h"
#include "LogFile.h"

//-----------------------------------------------------------------------
template <class T>
inline void SafeDelete(T *& ptr)
{
	if (ptr)
	{
		delete ptr;
		ptr = 0;
	}
}
///@} 

//------------------------------------------------------------------------
struct ReturnMessage
{
public:
//------------------------------------------------------------------------
	ReturnMessage () : code (-1) {}
	ReturnMessage (int code, const char* out)
	: code (code)
	{
		outString = out;
	}
	ReturnMessage (const ReturnMessage& other)
	: code (other.code)
	{
		outString = other.outString;
	}
	bool operator== (const ReturnMessage& other) const
	{
		return false;
	}

	void sendMessage ()
	{
		if (isEmpty ())
			return;
		String windowCaption ("BaseHead");

#if WINDOWS

		// The FindWindow function retrieves the handle to the top-level window
		// whose class name and/or window name match the specified strings.
		// This function does not search child windows.
		//
		HWND lResult = FindWindowA (NULL, windowCaption.text8 ());
		if (lResult)
		{
			static COPYDATASTRUCT cds;			// declare a variable with type copy-data-struct" (windows API)
			cds.dwData = code;			// acknowledge code
			cds.cbData = strlen (outString.text8 ());	// count of bytes in data block
			cds.lpData = (void*) outString.text8 ();			// pointer to data block

			SendMessage (lResult, WM_COPYDATA , -1, (LPARAM)&cds);
		}
#elif MAC
	#error	// Not implemented
#endif
	}

	bool isEmpty ()
	{
		return code == -1 && outString.isEmpty ();
	}
	
	int32 code;
	String outString;
};

//------------------------------------------------------------------------
class MessageSendThread : public FThread
{
public :
//------------------------------------------------------------------------
	static MessageSendThread* create () 
	{
		MessageSendThread* thread = NEW MessageSendThread;
		thread->setPriority (kLowPriority);
		thread->run ();
		return thread;
	}

	void addMessage (ReturnMessage message)
	{
		
		messageQueue.enqueue (message);
	}
	virtual void end () 
	{
		shutDown = true;

		messageQueueLock.lock ();
		while (!messageQueue.isEmpty ())
			messageQueue.dequeue ();
		messageQueueLock.unlock ();

		waitTimer.signalAll ();

		if (isRunning () && waitDead (1000) == false)
		{
			terminate ();
		}

		delete this;
	}

	uint32 entry ()
	{
		running = true;
		while (true)
		{
			if (shutDown)
				break;

			ReturnMessage currentMessage;
			getNextMessage (currentMessage);
			currentMessage.sendMessage ();

			setNextWaitTime ();
			waitTimer.waitTimeout (nextWaitTime);

		}
		running = false;
		return 0;
	}

	void getNextMessage (ReturnMessage& currentMessage ) 
	{
		messageQueueLock.lock ();
		if (!messageQueue.isEmpty ())
			currentMessage = messageQueue.dequeue ();			
		messageQueueLock.unlock ();
	}

	void setNextWaitTime() 
	{
		messageQueueLock.lock ();
		if (messageQueue.isEmpty () && !shutDown)
			nextWaitTime = 100;
		else
			nextWaitTime = 1;
		messageQueueLock.unlock ();
	}

private:
	MessageSendThread () : FThread ("BaseHeadMessageSendThread"), nextWaitTime (1), shutDown (false) {}
	virtual ~MessageSendThread () {}

	volatile bool shutDown;

	TQueue<ReturnMessage> messageQueue;
	FLock messageQueueLock;

	FCondition waitTimer;
	int32 nextWaitTime;
};

//------------------------------------------------------------------------
class MessageReceiveThread : public FThread
{
public:
//------------------------------------------------------------------------
	static MessageReceiveThread* create () 
	{
		MessageReceiveThread* thread = NEW MessageReceiveThread;
		thread->setPriority (kLowPriority);
		thread->initPipe ();
		thread->run ();
		return thread;
	}

	CNamedPipe* getPipe ()
	{
		return pipe;
	}

	virtual void end () 
	{
		shutDown = true;

		waitTimer.signalAll ();

		if (isRunning () && waitDead (1000) == false)
		{
			terminate ();
		}

		delete this;
	}

	uint32 entry ()
	{
		running = true;
		while (true)
		{
			if (shutDown)
				break;

			waitTimer.waitTimeout (40);

			if (!pipe)
				continue;

			string szMsg;
			bool result = pipe->read (szMsg);
			if (!result)
				continue;

			if (szMsg == "QUIT")
				break;

			if (!PipeMessageHandler::instance ())
				return 1;

			PipeMessageHandler::instance ()->readMessage (szMsg.c_str ());		
		}
		running = false;
		return 0;
	}
private:
	MessageReceiveThread () : FThread ("BaseHeadMessageReceiveThread"), shutDown (false), pipe (0) {}
	virtual ~MessageReceiveThread () { SafeDelete (pipe); }

	void initPipe ();

	volatile bool shutDown;
	FCondition waitTimer;

	CNamedPipe* pipe;
};

//------------------------------------------------------------------------
void MessageReceiveThread::initPipe ()
{
	pipe = NEW CNamedPipe ();
	pipe->SetPipeName (PIPE_NAME, ".");
	//m_Log->Write("Server pipe=%s", m_Pipe->GetRealPipeName(true).c_str());
	//m_Log->Write("Client pipe=%s", m_Pipe->GetRealPipeName(false).c_str());

	if (!pipe->initialize ())
	{
		// m_Log->Write("Failed to initialize pipe %s", m_Pipe->GetRealPipeName(true));
		delete pipe;
		pipe = NULL;
	}
}

//------------------------------------------------------------------------
PipeMessageHandler::PipeMessageHandler()
	: skiComponent (0)
	, lock (NEW FLock ("StateLock"))
	, isReceiving (false)
	, isShuttingDown (false)
	, waitForMessageReceiver (0, "PipeMessageHandler")
	, messageSendThread (0)
	, messageReceiveThread (0)
{

	messageReceiveThread = MessageReceiveThread::create ();
}

//------------------------------------------------------------------------
PipeMessageHandler::~PipeMessageHandler()
{
	if (messageReceiveThread)
	{
		messageReceiveThread->end ();
		messageReceiveThread = 0;
	}

	if (messageSendThread)
	{
		messageSendThread->end ();
		messageSendThread = 0;
	}

	SafeDelete (lock);
}

//------------------------------------------------------------------------
void PipeMessageHandler::setSkiComponent (SKIComponent* newSkiComponent )
{
	skiComponent = newSkiComponent;
}

//------------------------------------------------------------------------
void PipeMessageHandler::setShuttingDown()
{
	isShuttingDown = true;
}

//------------------------------------------------------------------------
void PipeMessageHandler::readMessage (const char *cmd )
{
	if (!skiComponent)
		return;

	bool canContinue = true;
	{
		FGuard guard (*lock);
		if (isShuttingDown)
			canContinue = false;
		else
			isReceiving = true;
	}
	if (canContinue)
	{

		FUnknownPtr<IMessenger> hostMessenger = FHostCreate (IMessenger, skiComponent->getHostClasses ());
		FUnknownPtr<IMessage> hostMessage = FHostCreate (IMessage, skiComponent->getHostClasses ());
		if (hostMessenger && hostMessage)
		{
			hostMessage->addString8 ("Command", cmd);
			// posted Messages get delivered in main thread
			hostMessenger->postMessage (skiComponent, hostMessage);		

			if (stricmp(cmd, "insert file") == 0)
			{
				// do not wait here because basehead seem to process the pasting
				this->resultMessage = "ok";
			}
			else
				waitForMessageReceiver.acquire ();
		}
	}
	else
		resultMessage = "Currently Sending Message";

	//resultMessage.toMultiByte ();
	//if (messageSendThread)
	//	messageReceiveThread->getPipe ()->send (resultMessage.text8 ());

	if (messageSendThread)
		messageReceiveThread->getPipe ()->send (resultMessage.c_str ());

	{
		FGuard guard (*lock);
		isReceiving = false;
	}

}

//------------------------------------------------------------------------------
void PipeMessageHandler::notifyMessageWasInterpreted (const char8* resultMessage)
{
	this->resultMessage = resultMessage;
	waitForMessageReceiver.release ();
}

//------------------------------------------------------------------------------
bool PipeMessageHandler::sendMessageToWindow (int code, const char* message )
{
	bool canContinue = true;
	{
		FGuard guard (*lock);
		if (isReceiving)
			canContinue = false;
	}

	if (canContinue)
	{
		if (0 == messageSendThread)
		{
			messageSendThread = MessageSendThread::create ();
		}		
		messageSendThread->addMessage (ReturnMessage (code, message));
	}

	return canContinue;
}
