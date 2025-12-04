//-----------------------------------------------------------------------------
// Project      : BaseHead
// Filename     : skicomponent.cpp
// Created by   : 
// Description  : 
//-----------------------------------------------------------------------------

#include "skicomponent.h"
#include "skiexampledialog.h"
#include "pluginterfaces/host/ihostclasses.h"
#include "pluginterfaces/host/ihostapplication.h"

#include "common/pattributes.h"
#include "base/source/tlist.h"
#include "base/source/tassociation.h"
#include "messagehandler.h"
#include "strutil.h"

extern void* moduleHandle; // defined in dllmain.cpp



#define HOST_NEW(Class) FHostCreate (Class, hostClasses);




static SKIComponent *m_Self = NULL;

//------------------------------------------------------------------------------
SKIComponent::SKIComponent ()
: guiDescription (0)
, projectInfo (0)
, dialogController (0)
, hostClasses (0)
{
	FUNKNOWN_CTOR

	// m_Log = NULL;
}

//------------------------------------------------------------------------------
SKIComponent::~SKIComponent ()
{
	FUNKNOWN_DTOR

	PipeMessageHandler::instance ()->setSkiComponent (0);

	// Close mutex handle
	char c[] = "BaseHeadNuendoMutex";
	WCHAR    name[20];
	memset(name, 0, sizeof(name));
	MultiByteToWideChar(0, 0, c, strlen(c), name, strlen(c) + 1);

	HANDLE hMutex = CreateMutexW(NULL, TRUE, (LPWSTR)name);
	if(GetLastError() == ERROR_ALREADY_EXISTS)
	{
		CloseHandle(hMutex);
	}

	// if (m_Log) delete m_Log;
}

//------------------------------------------------------------------------------
IMPLEMENT_REFCOUNT (SKIComponent)
tresult PLUGIN_API SKIComponent::queryInterface (FIDString iid, void** obj)
{
	QUERY_INTERFACE (iid, obj, ::FUnknown::iid, IPluginBase)
	QUERY_INTERFACE (iid, obj, ::IPluginBase::iid, IPluginBase)
	QUERY_INTERFACE (iid, obj, ::IActionHandler::iid, IActionHandler)
	QUERY_INTERFACE (iid, obj, ::IIdleHandler::iid, IIdleHandler)
	QUERY_INTERFACE (iid, obj, ::IProjectNotification::iid, IProjectNotification)
	QUERY_INTERFACE (iid, obj, ::IProjectNotification2::iid, IProjectNotification2)
	QUERY_INTERFACE (iid, obj, ::IProjectStorageNotification::iid, IProjectStorageNotification)
	QUERY_INTERFACE (iid, obj, ::IMessageReceiver::iid, IMessageReceiver)

    *obj = 0;
    return kNoInterface;
}

//------------------------------------------------------------------------------
tresult PLUGIN_API SKIComponent::initialize (FUnknown* context)
{	
	context->queryInterface (IHostClasses::iid, (void**)&hostClasses);
	if (!hostClasses)
		return kResultFalse;

	// load gui description
	guiDescription = HOST_NEW (IGuiDescription);
	if (!guiDescription)
		return kResultFalse;

	if (guiDescription->loadResource (moduleHandle, STR ("skin.xml")) != kResultTrue)
		return kResultFalse;

	// add menu item to project menu
	IHostMenuBar* menuBar = HOST_NEW (IHostMenuBar);
	if (menuBar)
	{
		// menuBar->addItem (moduleHandle, "Project", STR ("Basehead SKI Test"), 1, "SKI", "ShowDialog");
		// menuBar->addItem (moduleHandle, "Project", STR ("Basehead SKI Window Test"), 1, "SKI", "ShowPlugWindow");
		menuBar->release ();	
	}

	// install action handler
	IActionManager* actionManager = HOST_NEW (IActionManager);
	if (actionManager)
	{
		actionManager->addActionHandler (this);
		actionManager->release ();
	}
	

	projectInfo = HOST_NEW (IProjectInformation);
	if (projectInfo)
		projectInfo->registerNotification (this);

	// initiate idle calls from host 
	FInstancePtr<IPlatform> platform (hostClasses);
	if (platform)
		platform->addIdleHandler (this);

	PipeMessageHandler::instance ()->setSkiComponent (this);
	Alone ();

	//m_Log = new CLogFile("c:\\basehead.log", true, 1024 * 1024);
	//m_Log->Write("BaseHead SKI started %s", "");

	return kResultOk;
}


//------------------------------------------------------------------------
void SKIComponent::SendAcknowledge(int code, const char *message)
{
	PipeMessageHandler::instance()->sendMessageToWindow (code, message);
}


//------------------------------------------------------------------------
bool SKIComponent::Alone()
{
	char c[] = "BaseHeadNuendoMutex";
	WCHAR    name[20];
	memset(name, 0, sizeof(name));
	MultiByteToWideChar(0, 0, c, strlen(c), name, strlen(c) + 1);

	HANDLE hMutex = CreateMutexW(NULL, TRUE, (LPWSTR)name);
	if(GetLastError() == ERROR_ALREADY_EXISTS)
	{
		CloseHandle(hMutex);
		return false;
	}
	SendAcknowledge(SKI_PLG_STARTED, c); // ack: project added
	return true;
}

inline void replace(std::string& text, std::string s, std::string d)
{
  for(std::string::size_type index=text.find(s); index!=std::string::npos; index=text.find(s, index))
  {
    text.replace(index, s.length(), d);
    index+=d.length();
  }
}

//------------------------------------------------------------------------
void SKIComponent::ReadMessage(const char *cmd)
{
	string message;
	if (cmd == 0 || stricmp (cmd, "") == 0)
	{
		message.append ("Empty command");
		goto Quit;
	}

	//m_Log->Write("input %s", cmd);
	
	{
		IProject *project = projectInfo->getActiveProject();
		vector<string> items;
		vector<string> tokens = strutil::split(string(cmd), string("\t"));

		if (!project)
		{
			message.append("Couldn't open active project");
			goto Quit;
		}

		if (stricmp (tokens[0].c_str(), "insert file") == 0 && tokens.size () >= 2)
		{
			InsertPackage package;
			package.parseTokens (tokens);


			IWindow *window = project->getProjectWindow ();
			window->toFront ();

			FIDString resultMessage = insertFile (package);
			message.append (resultMessage);

			goto Quit;
		}

		if (stricmp(cmd, "insert file") == 0)
		{
			IWindow *window = project->getProjectWindow();
			window->toFront();

			tresult res = -1;
			IActionManager* actionManager = HOST_NEW (IActionManager);
			if (actionManager)
			{
				res = actionManager->performAction ("Edit", "Paste");
				actionManager->release ();
				message.append("ok");
			}
			else
				message.append("Couldn't initialize Action Manager");
			return;
		}

		if (stricmp(cmd, "project path") == 0)
		{
			char buffer[1024], out[1024];
			memset(buffer, 0, sizeof(buffer));
			IPath *path = project->getProjectPath();
			if (path)
			{
				path->getFullPath((tchar *)buffer);
				int count = WideCharToMultiByte(CP_UTF8, 0, (LPCWSTR)buffer, -1, 0, 0, 0, 0);
				if (count > 0)
				{
					WideCharToMultiByte(CP_UTF8, 0, (LPCWSTR)buffer, -1, out, count, 0, 0);
					out[count] = 0; // make the string null-terminated
					message.append(out);
				}
				else
					message.append(buffer);
			}
			else
				message.append("No active persistent project");
			goto Quit;
		}

		if (stricmp(tokens[0].c_str(), "xfertopool file") == 0)
		{
			IMediaPool *pool = project->getMediaPool();
			if (pool)
			{
				for (int i = 0; i < pool->countMediaItems(); i++)
				{
					IMedium *medium = pool->getMediumByIndex(i);
					if (medium)
					{
						IPath *path = medium->getFilePath();
						char buffer[1024], out[1024];

						if (path)
						{
							memset(buffer, 0, sizeof(buffer));
							path->getFullPath((tchar *)buffer);

							int count = WideCharToMultiByte(CP_UTF8, 0, (LPCWSTR)buffer, -1, 0, 0, 0, 0);
							if (count > 0)
							{
								WideCharToMultiByte(CP_UTF8, 0, (LPCWSTR)buffer, -1, out, count, 0, 0);
								out[count] = 0; // make the string null-terminated

								// m_Log->Write("pool[%d]=%s", i, out);
								items.push_back(string(out));
							}
						}
					}
				}

				for (uint32 i = 1; i < tokens.size(); i++)
				{
					bool bFound = false;
					for (uint32 j = 0; j < items.size(); j++)
					{
						if (stricmp(tokens[i].c_str(), items[j].c_str()) == 0)
						{
							bFound = true;
							break;
						}
					}

					// m_Log->Write("tokens[%d]=%s found=%d", i, tokens[i].c_str(), bFound);
					if (!bFound)
					{
						// Add file to pool
						IAudioClip* clip = HOST_NEW (IAudioClip);
						if (clip)
						{
							FUnknownPtr<IMedium> medium(clip);
							if (medium)
							{
								WCHAR name[1024];
								memset(name, 0, sizeof(name));
								MultiByteToWideChar(0, 0, tokens[i].c_str(), strlen(tokens[i].c_str()), name, strlen(tokens[i].c_str()) + 1);

								IPath *path = HOST_NEW (IPath);
								path->setFullPath(name, 0);
								medium->setFilePath(path);
								// path->release ();
							}

							tresult res = pool->addMedium(medium);
							if (!res)
								message.append("ok");
							else
								message.append("Couldn't add media to pool");
						}
						else
						{
							message.append("Couldn't create audio clip");
						}
					}
					else
					{
						//message.append("File already exists in pool");
						message.append("ok");
					}
				}
				replace(message, "ok", "");
				if (message.length() == 0)
					message.append("ok");
				goto Quit;
			}
		}
	}

	
	message.append("Unknown command: ");
	message.append(cmd);

	// Process message
Quit:
	String messageObject = (char*)message.data ();
	PipeMessageHandler::instance ()->notifyMessageWasInterpreted (messageObject.text8 ());
}


//------------------------------------------------------------------------------
tresult PLUGIN_API SKIComponent::terminate ()
{
	if (projectInfo)
	{
		projectInfo->unregisterNotification (this);

		for (int32 i = 0; i < projectInfo->countProjects (); i++)
		{
			IProject* p = projectInfo->getProject (i);
			if (p)
				p->unregisterStorageNotification (this);
		}

		projectInfo->release ();
	}

	PipeMessageHandler::instance ()->setShuttingDown ();

	char c[] = "SKI plugin stopped";
	SendAcknowledge(SKI_PLG_STOPPED, c); // ack: project added

	if (hostClasses)
	{
		FInstancePtr <IHostMenuBar> menuBar (hostClasses);
		if (menuBar)
		{
			extern void* moduleHandle;
			menuBar->cleanupMenu (moduleHandle);
		}

		FInstancePtr <IActionManager> actionManager (hostClasses);
		if (actionManager)
			actionManager->removeActionHandler (this);

		FInstancePtr<IPlatform> platform (hostClasses);
		if (platform)
			platform->removeIdleHandler (this);
	}

	

	if (guiDescription)
		guiDescription->release ();
	guiDescription = 0;

	hostClasses = 0;
	return kResultOk;
}

//------------------------------------------------------------------------------
void PLUGIN_API SKIComponent::onIdle ()
{
	// do any low priority peridic tasks here
}


//------------------------------------------------------------------------------
tresult SKIComponent::handleAction (FIDString category, FIDString name, bool checkOnly)
{
	if (category && name)
	{	
		if (strcmp (category, "SKI") == 0)
		{
			if (strcmp (name, "ShowDialog") == 0)
			{
				return showTestDialog (checkOnly);
			}
			else if (strcmp (name, "ShowPlugWindow") == 0)
			{
				return openTestWindow (checkOnly);			
			}			
		}
	}

	return kResultFalse;
}

//------------------------------------------------------------------------------
void PLUGIN_API SKIComponent::windowClosed (IWindow* w)
{
	if (dialogController && dialogController->getWindow () == w)
		dialogController = 0;
}


//------------------------------------------------------------------------------
tresult SKIComponent::showTestDialog (bool checkOnly)
{
	if (guiDescription)
	{
		if (checkOnly)
			return kResultTrue;

		if (dialogController && dialogController->getWindow ())
		{
			dialogController->getWindow ()->toFront ();
			return kResultTrue;
		}
		
		dialogController = new SKIDialogController (projectInfo, hostClasses);
		IWindow* window = 0;
		guiDescription->openWindow ("SKITest", dialogController, &window);
		dialogController->release (); // the window has got a reference to the controller

		if (window)
		{					
			dialogController->setWindow (window);
			window->addToDesktop ();
			window->addCloseNotification (this);
		}
		else
		{
			dialogController = 0;
			return kInternalError;
		}
		return kResultTrue;
	}

	return kInternalError;
}


//------------------------------------------------------------------------------
tresult SKIComponent::openTestWindow (bool checkOnly)
{
	if (!guiDescription)
		return kInternalError;

	if (checkOnly)
		return kResultTrue;
	
	SKITestViewController* controller = new SKITestViewController ();
	IWindow* window = 0;
	guiDescription->openWindow ("SKIViewTest", controller, &window);
	controller->release ();
		
	if (window)
	{		
		window->addToDesktop ();	
		window->addCloseNotification (this);
	}
		
	return kResultTrue;
}


//------------------------------------------------------------------------------
void SKIComponent::projectAdded (IProject* project)
{
	string message = "No active persistent project";

	char buffer[1024], out[1024];
	memset(buffer, 0, sizeof(buffer));
	IPath *path = project->getProjectPath();
	if (path)
	{
		path->getFullPath((tchar *)buffer);
		int count = WideCharToMultiByte(CP_UTF8, 0, (LPCWSTR)buffer, -1, 0, 0, 0, 0);
		if (count > 0)
		{
			WideCharToMultiByte(CP_UTF8, 0, (LPCWSTR)buffer, -1, out, count, 0, 0);
			out[count] = 0; // make the string null-terminated
			message = out;
		}
	}
	SendAcknowledge(SKI_PRJ_ADDED, message.c_str()); // ack: project added

	project->registerStorageNotification (this);
}

//------------------------------------------------------------------------------
void SKIComponent::projectRemoved (IProject* project)
{
	//static char m_SKIProject_Removed[2048];
	string message = "No active persistent project";

	char buffer[1024], out[1024];
	memset(buffer, 0, sizeof(buffer));
	IPath *path = project->getProjectPath();
	if (path)
	{
		path->getFullPath((tchar *)buffer);
		int count = WideCharToMultiByte(CP_UTF8, 0, (LPCWSTR)buffer, -1, 0, 0, 0, 0);
		if (count > 0)
		{
			WideCharToMultiByte(CP_UTF8, 0, (LPCWSTR)buffer, -1, out, count, 0, 0);
			out[count] = 0; // make the string null-terminated
			message = out;
		}
	}

	SendAcknowledge(SKI_PRJ_REMOVED, message.c_str()); // ack: project removed

	project->unregisterStorageNotification (this);
}

//------------------------------------------------------------------------------
tresult SKIComponent::canProjectClose (IProject* project)
{
	return kResultTrue;
}

//------------------------------------------------------------------------------
void SKIComponent::projectActivated (IProject* project)
{
	string message = "No active persistent project";

	char buffer[1024], out[1024];
	memset(buffer, 0, sizeof(buffer));
	IPath *path = project->getProjectPath();
	if (path)
	{
		path->getFullPath((tchar *)buffer);
		int count = WideCharToMultiByte(CP_UTF8, 0, (LPCWSTR)buffer, -1, 0, 0, 0, 0);
		if (count > 0)
		{
			WideCharToMultiByte(CP_UTF8, 0, (LPCWSTR)buffer, -1, out, count, 0, 0);
			out[count] = 0; // make the string null-terminated
			message = out;
		}
	}

	SendAcknowledge(SKI_PRJ_ACTIVATED, message.c_str()); // ack: project activated
}

//------------------------------------------------------------------------------
void SKIComponent::projectDeactivated (IProject* project)
{
	//static char m_SKIProject_Deactivated[2048];
	string message = "No active persistent project";

	char buffer[1024], out[1024];
	memset(buffer, 0, sizeof(buffer));
	IPath *path = project->getProjectPath();
	if (path)
	{
		path->getFullPath((tchar *)buffer);
		int count = WideCharToMultiByte(CP_UTF8, 0, (LPCWSTR)buffer, -1, 0, 0, 0, 0);
		if (count > 0)
		{
			WideCharToMultiByte(CP_UTF8, 0, (LPCWSTR)buffer, -1, out, count, 0, 0);
			out[count] = 0; // make the string null-terminated
			message = out;
		}
	}

	SendAcknowledge(SKI_PRJ_DEACTIVATED, message.c_str()); // ack: project deactivated

	// save the state to able to restore it when the project is reactivated
	storeSetup (project);
}

//------------------------------------------------------------------------------
void SKIComponent::beforeProjectActivation (IProject* project)
{
	restoreSetup (project);

	string message = "No active persistent project";

	char buffer[1024], out[1024];
	memset(buffer, 0, sizeof(buffer));
	IPath *path = project->getProjectPath();
	if (path)
	{
		path->getFullPath((tchar *)buffer);
		int count = WideCharToMultiByte(CP_UTF8, 0, (LPCWSTR)buffer, -1, 0, 0, 0, 0);
		if (count > 0)
		{
			WideCharToMultiByte(CP_UTF8, 0, (LPCWSTR)buffer, -1, out, count, 0, 0);
			out[count] = 0; // make the string null-terminated
			message = out;
		}
	}

	SendAcknowledge(SKI_PRJ_ACTIVATED, message.c_str()); // ack: project activated
}

//------------------------------------------------------------------------------
void SKIComponent::beforeProjectSaved (IProject* project)
{
	char buffer[1024], out[1024];
	memset(buffer, 0, sizeof(buffer));
	IPath *path = project->getProjectPath();
	string message = "No active persistent project";

	if (path)
	{
		path->getFullPath((tchar *)buffer);
		int count = WideCharToMultiByte(CP_UTF8, 0, (LPCWSTR)buffer, -1, 0, 0, 0, 0);
		if (count > 0)
		{
			WideCharToMultiByte(CP_UTF8, 0, (LPCWSTR)buffer, -1, out, count, 0, 0);
			out[count] = 0; // make the string null-terminated
			message = out;
		}
	}

	SendAcknowledge(SKI_PRJ_ACTIVATED, message.c_str()); // ack: project activated

	storeSetup (project);
}

//------------------------------------------------------------------------------
int32 PLUGIN_API SKIComponent::notifyMessage( IMessage* message )
{
	if (!message)
		return kMessageUnknown;

	ReadMessage (message->getString8 ("Command"));
	return kMessageNotified;
}

//------------------------------------------------------------------------------
IHostClasses* SKIComponent::getHostClasses()
{
	return hostClasses;
}



//------------------------------------------------------------------------------
// example how to store setup data in the project
void SKIComponent::storeSetup (IProject* project)
{
	FUnknownPtr<IProjectObject> obj (project);
	if (obj)
	{
		IAttributes* attr = HOST_NEW (IAttributes);
		if (attr)
		{
			PAttributes::set (attr, "My Data", 99.0);
			obj->setUserAttribute ("MySt", attr, true);

		}
	}
}

//------------------------------------------------------------------------------
// example how to restore setup data in the project
void SKIComponent::restoreSetup (IProject* project)
{
	FUnknownPtr<IProjectObject> obj (project);
	if (obj)
	{
		FVariant var;
		if (obj->getUserAttribute ("My Setup", var) == kResultTrue)
		{
			FUnknownPtr<IAttributes> attr (var.getObject ());
			if (attr)
			{
				// restore setup....
				double something = 0.0;
				if (PAttributes::get (attr, "My Data", something))
				{
					//...
				}
			}
		}
	}
}

//------------------------------------------------------------------------------
FIDString SKIComponent::insertFile (InsertPackage& package)
{
	OPtr<IPath> path = HOST_NEW (IPath);
	if (path)
		path->setFullPath (package.pathString.text (), IPath::kIPFile);

	IProject* project = projectInfo->getActiveProject();
	ASSERT (project)

	// Find Medium or create new one
	IMediaPool* pool = project->getMediaPool ();
	if (!pool)
		return "Access to pool failed";
	
	FUnknownPtr<IAudioClip> clip;
	IMedium* medium = pool->getMediumByPath (path);
	if (medium)
	{
		clip = medium;
	}
	if (!medium)
	{
		clip = HOST_NEW (IAudioClip);
		if (clip)
		{
			FUnknownPtr<IMedium> newMedium (clip);
			if (newMedium)
			{
				newMedium ->setFilePath (path);
				medium = newMedium;
				pool->addMedium (medium);
			}
		}
	}
	if (!medium)
		return "No pool medium can be created";

	// Find selected AudioEvent
	FUnknownPtr<IProjectObject> projectAsObject (project);
	if (!projectAsObject)
		return "Fail";
	
	IProjectObject* firstSelectedAudioTrack = findDestinationAudioTrack (projectAsObject, package.trackOffset);
	if (!firstSelectedAudioTrack)
		return "No audio track selected or no audio track available";

	// Get cursor time
	double insertTime = package.cursorOffset;
	OPtr<ITransportDevice> transportDevice = HOST_NEW (ITransportDevice);
	if (transportDevice)
		insertTime += transportDevice->getDisplayPosition ();


	// Create Event and insert into project

	OPtr<IProjectContext> trackContext = project->createContext (firstSelectedAudioTrack);
	IAudioEvent* audioEvent = HOST_NEW (IAudioEvent);
	FUnknownPtr<IProjectObject> audioObj (audioEvent);
	if (!audioEvent || !audioObj)
		return "Audio event cannot be created";
	
	audioEvent->setMedium (trackContext, clip);
	audioObj->setStartPosition (trackContext, insertTime);
	if (package.inTime > 0.0)
		audioObj->setDataOffset (trackContext, package.inTime);
	if (package.length > 0.0)
		audioObj->setEndPosition (trackContext, insertTime+package.length);
	if (!package.description.isEmpty ())
		audioEvent->setDescription (trackContext, package.description.text ());
	audioObj->setSelected (trackContext, true);

	OPtr<IProjectEdit> edit = HOST_NEW (IProjectEdit);
	if (!edit)
		return "Undo Object cannot be created";
	edit->setEditMode (IProjectEdit::kBulkMode);
	edit->insertObject (trackContext, audioEvent);
	edit->finish (project, STR ("Insert File from BaseHead"));

	return "ok";
}

//------------------------------------------------------------------------------
IProjectObject* SKIComponent::findDestinationAudioTrack (IProjectObject* parent, uint32 trackOffset)
{
	int32 counter = -1;
	return findDestinationAudioTrack (parent, trackOffset, counter);
}

//------------------------------------------------------------------------------
IProjectObject* SKIComponent::findDestinationAudioTrack (IProjectObject* parent, uint32 trackOffset, int32& counter)
{
	OPtr<IProjectIterator> iter = parent->createIterator ();
	if (iter)
	{
		while (!iter->done ())
		{
			IProjectObject* subObject = iter->getNextObject ();
			if (subObject)
			{
				if (subObject->isObjectType (kFolderObject))
				{
					IProjectObject* result = findDestinationAudioTrack (subObject, trackOffset, counter);
					if (result)
						return result;
				}
				if (subObject->isObjectType (kAudioObject))
				{
					if (counter < 0)
					{
						if (subObject->isSelected ())
						{
							counter = 0;
							if (trackOffset == 0)
								return subObject;
						}
					}
					else
					{
						counter++;
						if (counter == trackOffset)
							return subObject;
					}
					
				}
			}
		}
	}
	return 0;
}

//------------------------------------------------------------------------
InsertPackage::InsertPackage ()
: cursorOffset (0.0)
, trackOffset (0)
, inTime (-1.0)
, length (-1.0)
{
}

//------------------------------------------------------------------------
void InsertPackage::parseTokens (std::vector<string>& tokens )
{
#if DEVELOPMENT
	pathString = "c:\\fun\\Gitarre - Riff1.wav";
	description = "Awesome Name";
	trackOffset = 3;
	cursorOffset = 5;
	inTime = 0.5;
	//outTime = 2;
#else
	String tempString;

	if (tokens.size () >= 2)
		pathString = tokens[1].c_str();
	if (tokens.size () >= 3)
		description = tokens[2].c_str();
	if (tokens.size () >= 4)
	{
		tempString = tokens[3].c_str();
		tempString.scanUInt32 (trackOffset);
	}
	if (tokens.size () >= 5)
	{
		tempString = tokens[4].c_str();
		tempString.scanFloat (cursorOffset);
	}
	if (tokens.size () >= 6)
	{
		tempString = tokens[5].c_str();
		tempString.scanFloat (inTime);
	}
	if (tokens.size () >= 7)
	{
		tempString = tokens[6].c_str();
		tempString.scanFloat (length);
	}
#endif
}
