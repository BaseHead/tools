//-----------------------------------------------------------------------------
// Project      : BaseHeadSKI
// Filename     : skicomponent.h
// Created by   : Mario Ewald
// Description  : Compenent class
//-----------------------------------------------------------------------------

#ifndef __skicomponent__
#define __skicomponent__

#include "pluginterfaces/base/ipluginbase.h"
#include "pluginterfaces/base/ipersistent.h"
#include "pluginterfaces/host/frame.h"
#include "pluginterfaces/host/frame/ipath.h"
#include "pluginterfaces/host/project/iprojectinfo.h"
#include "pluginterfaces/gui/iplugcontroller.h"
#include "pluginterfaces/host/devices/itransportdevice.h"

#include "common/pvaluecontainer.h"
#include "base/source/fobject.h"
#include "base/source/fstring.h"

#include <vector>


class SKIDialogController;
namespace Steinberg {
class IHostClasses;
class IProjectObject;
}
using namespace Steinberg;


//------------------------------------------------------------------------
struct InsertPackage 
{
	String pathString;
	String description;
	uint32 trackOffset;
	double cursorOffset;
	double inTime;
	double length;

	InsertPackage ();

	void parseTokens (std::vector<std::string>& tokens);
};


//------------------------------------------------------------------------
class SKIComponent : public IPluginBase, 
				     public IActionHandler,	
					 public ICloseWindowNotification,
					 public IIdleHandler,
					 public IProjectNotification2,
					 public IProjectStorageNotification,
					 public IMessageReceiver
{
public:
//------------------------------------------------------------------------
	SKIComponent ();
	~SKIComponent ();
	
	static FUnknown* newInstance (void*){return (IPluginBase*) new SKIComponent;}

	//IPluginBase
	virtual tresult PLUGIN_API initialize (FUnknown* context);
	virtual tresult PLUGIN_API terminate ();

	// IActionHandler
	virtual tresult PLUGIN_API handleAction (FIDString category, FIDString name, bool checkOnly);

	// ICloseWindowNotification
	virtual void PLUGIN_API windowClosed (IWindow*);

	// IIdleHandler
	virtual void PLUGIN_API onIdle ();

	// IProjectNotification2
	virtual void projectActivated (IProject* project);
	virtual void projectDeactivated (IProject* project);
	virtual void projectAdded (IProject* project);
	virtual void projectRemoved (IProject* project);
	virtual tresult canProjectClose (IProject* project);
	virtual void beforeProjectActivation (IProject* project);

	// IProjectStorageNotification
	virtual void beforeProjectSaved (IProject* project);

	// IMessageReceiver
	virtual int32 PLUGIN_API notifyMessage (IMessage* message);


	IHostClasses* getHostClasses ();

	// CLogFile *m_Log;
	void ReadMessage(const char *message);

	DECLARE_FUNKNOWN_METHODS
protected:
	IHostClasses* hostClasses;
	IProjectInformation* projectInfo;
	IGuiDescription* guiDescription;
	SKIDialogController* dialogController;

	tresult showTestDialog (bool checkOnly);
	tresult openTestWindow (bool checkOnly);

	void storeSetup (IProject* project);
	void restoreSetup (IProject* project);
	bool Alone ();
	void SendAcknowledge (int code, const char *message);
	FIDString insertFile (InsertPackage& package);

	IProjectObject* findDestinationAudioTrack (IProjectObject* parent, uint32 trackOffset);
	IProjectObject* findDestinationAudioTrack (IProjectObject* parent, uint32 trackOffset, int32& counter);

};




#endif