//-----------------------------------------------------------------------------
// Project      : SKI Example
// Filename     : skiexampledialog.h
// Created by   : Mario Ewald
// Description  : Compenent class
//-----------------------------------------------------------------------------

#ifndef __skiexampledialog__
#define __skiexampledialog__
#define IVALUE_RENAME 1

#include "pluginterfaces/gui/iplugcontroller.h"
#include "pluginterfaces/host/frame.h"
#include "pluginterfaces/host/ski.h"
#include "pluginterfaces/base/fstrdefs.h"

#include "common/pvaluecontainer.h"
#include "pluginterfaces/base/icloneable.h"

namespace Steinberg {
class IProjectInformation;
class IHostClasses;
class IWindow;
class ISkiAutomationNode;
}
using namespace Steinberg;

//------------------------------------------------------------------------
class SKIDialogController : public IPlugController, public IDependent
{
public:
//------------------------------------------------------------------------
	SKIDialogController (IProjectInformation* projectInfo, IHostClasses* hostClasses);
	~SKIDialogController ();
	
	//IPlugController
	virtual tresult PLUGIN_API getParameter (const char* name, IParameter**);
	virtual tresult PLUGIN_API parameterChanged (IParameter*, int32 tag);

	void PLUGIN_API update (FUnknown* changedUnkown, int32 msg);

	IWindow* getWindow () const {return window;}
	void setWindow (IWindow* w) {window = w;}

	DECLARE_FUNKNOWN_METHODS
protected:
	IProjectInformation* projectInfo;
	IWindow* window;
	IHostClasses* hostClasses;
	PValueContainer values;
	IValue* monitorValue;

	IDevice* getVstChannelNode ();
	IDevice* getMidiChannelNode ();

	ITrack* getFirstAudioTrack ();

	void projectZoomTest ();
	void projectTest1 ();
	void projectTest2 ();
	void projectSelectionTest ();
	void deviceTest ();
	void undoTest ();
	void colorTest ();
	void automationTest ();
	void cloneableTest ();

	void loadAsioDriver ();
	void setMidiPortNames ();
	void hideMidiPorts ();
	void setupAudioPorts ();
	void createOutputChannel (); 
	void connectAudioChannels ();
	void connectAudioSends ();
	void connectMidiChannels ();
	void checkPortsOfAudioChannels ();
	void makeMonitorDependency ();
	void removeMonitorDependency (bool release);

	void createGroupTrack ();
	void createHugeBus ();
};


//------------------------------------------------------------------------
class SKITestViewController : public IPlugController, public IViewBuilder, public IMessageReceiver
{
public:
	SKITestViewController ();
	~SKITestViewController ();
	// IPlugController
	virtual tresult PLUGIN_API getParameter (const char* name, IParameter** paramResult) { return kNotImplemented; } 
	virtual tresult PLUGIN_API parameterChanged (IParameter* parameter, int32 tag) { return kNotImplemented; } 
	
	// IViewBuilder
	virtual tresult PLUGIN_API createView (const char* name, ViewRect* rect, FUnknown** view /*out*/);

	// IMessageReceiver
	virtual tresult PLUGIN_API notifyMessage (IMessage* message);

	DECLARE_FUNKNOWN_METHODS
};


//------------------------------------------------------------------------------
// TestDeviceNode
//------------------------------------------------------------------------------

//------------------------------------------------------------------------------
class TestDeviceNode : public IParameterDefinition, public IPlugController
{
public:
//------------------------------------------------------------------------------
	TestDeviceNode ();
	~TestDeviceNode ();
	DECLARE_FUNKNOWN_METHODS

	static FUnknown* newInstance (void*){return (IParameterDefinition*) new TestDeviceNode;}
	static FUID classID;
		
	virtual void PLUGIN_API setOwner (FUnknown* owner); // !!! must not add ref!!!!!
	virtual const tchar* PLUGIN_API getTitle ()  {return STR ("SKI Node");}
	virtual const FUID& PLUGIN_API getClassID () {return classID;}
	virtual int32 PLUGIN_API getParameterCount ();
	virtual bool PLUGIN_API getParameterInfo (int32 paramIndex, ParamInfo& info);

	virtual bool PLUGIN_API valueToString (ParamID id, ParamValue valueNormalized, IStringResult& result);
	virtual bool PLUGIN_API stringToValue (ParamID id, const tchar* string, ParamValue& valueNormalized);

	virtual ParamValue PLUGIN_API normalizedToPlain (ParamID id, ParamValue valueNormalized);
	virtual ParamValue PLUGIN_API plainToNormalized (ParamID id, ParamValue plainValue);

	virtual ParamValue PLUGIN_API getDisplayValue (ParamID id);
	virtual bool PLUGIN_API setDisplayValue (ParamID id, ParamValue valueNormalized);

	void writeParameterTest ();

	void setParam1Value (IValue* v);

	// IPlugController
	virtual tresult PLUGIN_API getParameter (FIDString name, IParameter** paramResult) { return kNotImplemented; } 
	virtual tresult PLUGIN_API parameterChanged (IParameter* parameter, int32 tag); 

	enum ParamIDs
	{
		kIDParam1 = 1
	};
//------------------------------------------------------------------------------
private:
	double paramValue1;
	IValue* param1Value;
	
	ISkiAutomationNode* automationNode;
};



#endif
