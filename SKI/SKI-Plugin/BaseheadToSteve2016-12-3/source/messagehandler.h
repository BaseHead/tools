//------------------------------------------------------------------------
//
// Project     :
// Filename    : messagehandler.h
// Created by  : Bodo
// Description : Wrapper for message sending to and receiving from 
//				 BaseHead application
//
//------------------------------------------------------------------------
#include "base/source/fobject.h"
#include "base/source/tlist.h"

#include "NamedPipe.h"
#include <base/thread/include/fthread.h>
#include <base/thread/include/flock.h>


#define PIPE_NAME "BaseHeadNuendoPipe"

#define SKI_PLG_STARTED		0
#define SKI_PRJ_ADDED		1
#define SKI_PRJ_REMOVED		2
#define SKI_PRJ_ACTIVATED	3
#define SKI_PRJ_DEACTIVATED	4
#define SKI_PLG_STOPPED		5

class MessageSendThread;
class MessageReceiveThread;
class SKIComponent;

using namespace Steinberg;


//------------------------------------------------------------------------
class PipeMessageHandler : public FObject
{
public:
	//--------------------------------------------------------------------
	PipeMessageHandler ();
	virtual ~PipeMessageHandler ();

	void setSkiComponent (SKIComponent* newSkiComponent);
	void setShuttingDown ();

	void readMessage (const char *cmd);
	bool sendMessageToWindow (int code, const char *message);

	void notifyMessageWasInterpreted (const char8* resultMessage);

	SINGLETON (PipeMessageHandler);
	//------------------------------------------------------------------------------
private:
	SKIComponent* skiComponent;
	
	FLock* lock;
	volatile bool isReceiving;
	volatile bool isShuttingDown;

	FSemaphore waitForMessageReceiver;
	//String resultMessage;
	string resultMessage;

	MessageSendThread* messageSendThread;
	MessageReceiveThread* messageReceiveThread;
};