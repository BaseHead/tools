#include "skiexampledialog.h"

#include "pluginterfaces/host/frame.h"  
#include "pluginterfaces/host/ihostclasses.h"
#include "pluginterfaces/base/istringresult.h"
#include "pluginterfaces/base/keycodes.h" 

#include "common/pluginview_old.h" 
#include "base/source/fstring.h"
#include "base/source/tarray.h"

#include <stdio.h>
#include <ctype.h>

#define HOST_NEW(Class) FHostCreate (Class, hostClasses);

#ifndef STR
#define STR(s) s
#endif

//------------------------------------------------------------------------------
// edit step example class
// (does nothing sensible, only displays a message box...)
//------------------------------------------------------------------------------
class TestEditStep : public IEditStep
{
public: 
	TestEditStep (IHostClasses* hc) : hostClasses (hc) 
	{
		FUNKNOWN_CTOR
	}
	~TestEditStep ()
	{
		FUNKNOWN_DTOR
	}
	DECLARE_FUNKNOWN_METHODS

	virtual tresult PLUGIN_API execute ()
	{
		FInstancePtr <IAlert> alert (hostClasses);
		if (alert)
			alert->warn (STR ("Edit Step Excuted"));
		return kResultOk;
	}
	virtual void PLUGIN_API undo ()
	{
		FInstancePtr <IAlert> alert (hostClasses);
		if (alert)
			alert->warn (STR ("Edit Step Undone"));
	}
	virtual void PLUGIN_API redo ()
	{
		FInstancePtr <IAlert> alert (hostClasses);
		if (alert)
			alert->warn (STR ("Edit Step Redone"));
	}

	virtual tresult PLUGIN_API getAffected (IList* projectObjects)
	{
		return kNotImplemented;
	}

	IHostClasses* hostClasses;
};
IMPLEMENT_FUNKNOWN_METHODS (TestEditStep, IEditStep, IEditStep::iid)

//------------------------------------------------------------------------------
// Demonstrate a cloneable attribute
//------------------------------------------------------------------------------
class SkiCloneable : public ICloneable
{
public: 
	SkiCloneable (IHostClasses* hc) : hostClasses (hc) 
	{
		FUNKNOWN_CTOR
	}
	SkiCloneable (const SkiCloneable& other)
	: hostClasses (other.hostClasses)
	{
		FUNKNOWN_CTOR
	}
	~SkiCloneable ()
	{
		FUNKNOWN_DTOR
	}
	virtual FUnknown* PLUGIN_API clone ()
	{
		FInstancePtr <IAlert> alert (hostClasses);
		if (alert)
			alert->warn (STR ("I have been cloned !"));
	
		return new SkiCloneable (*this);
	}

	DECLARE_FUNKNOWN_METHODS

protected:
	IHostClasses* hostClasses;
};
IMPLEMENT_FUNKNOWN_METHODS (SkiCloneable, ICloneable, ICloneable::iid)


//------------------------------------------------------------------------------
//------------------------------------------------------------------------------
FUID TestDeviceNode::classID (0xFFD36D77, 0x8A5C462A, 0xA2554D16, 0xE829537E);

IMPLEMENT_REFCOUNT (TestDeviceNode)
tresult PLUGIN_API TestDeviceNode::queryInterface (FIDString iid, void** obj)
{
	QUERY_INTERFACE (iid, obj, ::FUnknown::iid, IParameterDefinition)
	QUERY_INTERFACE (iid, obj, IPlugController::iid, IPlugController)
	QUERY_INTERFACE (iid, obj, IParameterDefinition::iid, IParameterDefinition)
	
    *obj = 0;
    return kNoInterface;
}

//------------------------------------------------------------------------------
TestDeviceNode::TestDeviceNode ()
: paramValue1 (50.0)
, automationNode (0)
, param1Value (0)
{
	FUNKNOWN_CTOR
}

//------------------------------------------------------------------------------
TestDeviceNode::~TestDeviceNode ()
{
	if (param1Value)
	{
		param1Value->connect (0, 0);
		param1Value->release ();
	}
	FUNKNOWN_DTOR
}

//------------------------------------------------------------------------------
void TestDeviceNode::setParam1Value (IValue* v)
{
	if (v != param1Value)
	{
		if (param1Value)
			param1Value->release ();
		param1Value = v;
		if (param1Value)
		{
			param1Value->addRef ();
			param1Value->connect (this, kIDParam1);
		}
	}
}

//------------------------------------------------------------------------------
tresult PLUGIN_API TestDeviceNode::parameterChanged (IParameter* parameter, int32 tag)
{
	if (tag == kIDParam1 && param1Value)
	{
		if (automationNode)
			automationNode->writeAutomation (param1Value->getTag (), param1Value->getNormalized (), param1Value->isEditLocked ());
		return kResultTrue;
	}
	return kResultFalse;
}


//------------------------------------------------------------------------------
void PLUGIN_API TestDeviceNode::setOwner (FUnknown* owner)
{
	automationNode = FUnknownPtr<ISkiAutomationNode> (owner);
}

//------------------------------------------------------------------------------
int32 PLUGIN_API TestDeviceNode::getParameterCount ()
{
	return 1;
}

//------------------------------------------------------------------------------
bool PLUGIN_API TestDeviceNode::getParameterInfo (int32 paramIndex, ParamInfo& info)
{
	if (paramIndex == 0)
	{
		info.id = kIDParam1;
		info.stepCount = 0;
		tstrcpy (info.title, STR ("SKI Test Param 1"));
		return true;
	}
	return false;
}

//------------------------------------------------------------------------------
bool PLUGIN_API TestDeviceNode::valueToString (ParamID id, ParamValue valueNormalized, IStringResult& result)
{
	if (id == kIDParam1)
	{
		String text;
		text.printf (STR ("%.2f"), normalizedToPlain (id, valueNormalized));
		result.setText (text);
		return true;
	}
	return false;
}

//------------------------------------------------------------------------------
bool PLUGIN_API TestDeviceNode::stringToValue (ParamID id, const tchar* string, ParamValue& valueNormalized)
{
	if (id == kIDParam1)
	{
		double input = 0;
		ConstString (string).scanFloat (input);
		valueNormalized = input;
		return true;
	}	
	return false;
}

//------------------------------------------------------------------------------
ParamValue PLUGIN_API TestDeviceNode::normalizedToPlain (ParamID id, ParamValue valueNormalized)
{
	if (id == kIDParam1)
	{
		return valueNormalized * 100.0;
	}	
	return valueNormalized;
}

//------------------------------------------------------------------------------
ParamValue PLUGIN_API TestDeviceNode::plainToNormalized (ParamID id, ParamValue plainValue)
{
	if (id == kIDParam1)
	{
		return plainValue / 100.0;
	}	
	return plainValue;
}

//------------------------------------------------------------------------------
ParamValue PLUGIN_API TestDeviceNode::getDisplayValue (ParamID id)
{
	if (id == kIDParam1)
		return plainToNormalized (kIDParam1, paramValue1);
	return 0.0;
}

//------------------------------------------------------------------------------
bool PLUGIN_API TestDeviceNode::setDisplayValue (ParamID id, ParamValue valueNormalized)
{
	if (id == kIDParam1)
	{
		paramValue1 = normalizedToPlain (id, valueNormalized);
		if (param1Value)
			param1Value->setValueFloat ((float)paramValue1);
		return true;
	}
	return false;
}
	
//------------------------------------------------------------------------------
void TestDeviceNode::writeParameterTest ()
{
	if (automationNode)
	{
		double start = 5.0; // seconds
		double paramNormalized = 0;
		if (automationNode->writeAutomationAt (start, kIDParam1, 0.5, true) != kResultTrue)
			return;
		
		for (int32 i = 0; i < 20; i++)
		{
			int32 value = i * 8;
			while (value > 100)
				value -= 100;
			paramNormalized = ((double)value) / 100.0;
			automationNode->writeAutomationAt (start, kIDParam1, paramNormalized, true);
			start += 0.5;
		}
		
		automationNode->writeAutomationAt (start, kIDParam1, paramNormalized, false, true);
	}
}




//------------------------------------------------------------------------------
// dialog controller
//------------------------------------------------------------------------------
enum
{
	kCommandTestTag,
	kProjectWindowZoomTag,
	kProjectTest1Tag,
	kProjectTest2Tag,
	kProjectTest3Tag,

	kCursorTestTag,
	kModalTestTag,
	kModalTest2Tag,
	kDeviceTestTag,
	kUndoTestTag,
	kColorTestTag,
	kDeviceAutomationTestTag,
	kCloneableTestTag,

	kLoadAsioDriverTag,
	kSetMidiPortNamesTag,
	kHideMidiPortsTag,
	kSetupAudioPortsTag,
	kCreateOutputChannelTag,
	kConnectAudioChannelsTag,
	kConnectAudioSendsTag,
	kCheckAudioPortsTag,
	kMonitorDependecyTag,
	kConnectMidiChannelsTag,

	kCreateGroupTrackTag,
	kCreateHugeBusTag,
};

//------------------------------------------------------------------------------
SKIDialogController::SKIDialogController (IProjectInformation* pi, IHostClasses* hostClasses)
: projectInfo (pi)
, window (0)
, hostClasses (hostClasses)
, values (hostClasses)
, monitorValue (0)
{
	FUNKNOWN_CTOR

	IValue* value;
	values.setController (this);

	values.addOnOffValue (kCommandTestTag, "commandTest");
	values.addOnOffValue (kProjectWindowZoomTag, "projectZoom");
	values.addOnOffValue (kProjectTest1Tag, "projectTest1");
	values.addOnOffValue (kProjectTest2Tag, "projectTest2");
	values.addOnOffValue (kProjectTest3Tag, "projectTest3");
	values.addOnOffValue (kCursorTestTag, "cursortest");
	values.addOnOffValue (kModalTestTag, "modaltest");
	values.addOnOffValue (kModalTest2Tag, "modaltest2");
	values.addOnOffValue (kDeviceTestTag, "deviceTest");
	values.addOnOffValue (kUndoTestTag, "undoTest");
	values.addOnOffValue (kColorTestTag, "colorTest");
	values.addOnOffValue (kDeviceAutomationTestTag, "automation");
	values.addOnOffValue (kCloneableTestTag, "cloneable");

	values.addFloatValue (TestDeviceNode::kIDParam1, "testParam", 0, 100.f, 50., -1, true);

	values.addOnOffValue (kLoadAsioDriverTag, "loadAsioDriver");
	values.addOnOffValue (kSetMidiPortNamesTag, "setMidiPortNames");
	values.addOnOffValue (kHideMidiPortsTag, "hideMidiPorts");
	values.addOnOffValue (kSetupAudioPortsTag, "setupAudioPorts");
	values.addOnOffValue (kCreateOutputChannelTag, "createOutput");
	values.addOnOffValue (kConnectAudioChannelsTag, "connectAudioChannels");
	values.addOnOffValue (kConnectAudioSendsTag, "connectAudioSends");
	values.addOnOffValue (kCheckAudioPortsTag, "checkAudioPorts");
	values.addOnOffValue (kMonitorDependecyTag, "monitorDependency");
	values.addOnOffValue (kConnectMidiChannelsTag, "connectMidiChannels");
	
	values.addOnOffValue (kCreateGroupTrackTag, "createGroupTrack");
	values.addOnOffValue (kCreateHugeBusTag, "createHugeBus");


	FInstancePtr <ITransportDevice> tDevice (hostClasses);
	if (tDevice)
	{
		value = tDevice->createParamInterface ("stop");
		if (value)
			values.addExternValue (value, "stop");

		value = tDevice->createParamInterface ("start");
		if (value)
			values.addExternValue (value, "play");
	}
}

//------------------------------------------------------------------------------
SKIDialogController::~SKIDialogController ()
{
	removeMonitorDependency (true);
	FUNKNOWN_DTOR
}

//------------------------------------------------------------------------------
IMPLEMENT_REFCOUNT (SKIDialogController)
tresult PLUGIN_API SKIDialogController::queryInterface (FIDString iid, void** obj)
{
	QUERY_INTERFACE (iid, obj, FUnknown::iid, IPlugController)
	QUERY_INTERFACE (iid, obj, IPlugController::iid, IPlugController)
	QUERY_INTERFACE (iid, obj, IDependent::iid, IDependent)

    *obj = 0;
    return kNoInterface;
}

//IPlugController
//------------------------------------------------------------------------------
tresult PLUGIN_API SKIDialogController::getParameter (FIDString name, IParameter** p)
{
	*p = values.getValue (name);
	if (*p)
		return kResultTrue;
	return kResultFalse;
}

//------------------------------------------------------------------------------
tresult PLUGIN_API SKIDialogController::parameterChanged (IParameter* p, int32 tag)
{
	switch (tag)
	{
		// send open file command
		case kCommandTestTag:
			if (p->getValueInt () > 0)
			{
				FInstancePtr <IActionManager> actionManager (hostClasses);
				if (actionManager)
					actionManager->performAction ("File", "Open");
			}	
			break;

		case kProjectWindowZoomTag:
			if (p->getValueInt () > 0)
				projectZoomTest ();
			break;

		case kProjectTest1Tag:
			if (p->getValueInt () > 0)
				projectTest1 ();
			break;

		case kProjectTest2Tag:
			if (p->getValueInt () > 0)
				projectTest2 ();
			break;

		case kProjectTest3Tag:
			if (p->getValueInt () > 0)
				projectSelectionTest ();
			break;

		// set/reset wait cursor
		case kCursorTestTag:
			{
				FInstancePtr <IPlatform> platform (hostClasses);
				if (platform)
					platform->setWaitCursor (p->getValueInt () > 0);
			}	
			break;

		case kModalTestTag:
			{
				FInstancePtr <IPlatform> platform (hostClasses);
				if (platform)
				{
					platform->beginPlugModal (STR ("Wait for about 10 seconds"));
					int32 begin = platform->getTickCount ();
					while (platform->getTickCount () - begin < 10000 
						&& platform->isInModalMode ())
						platform->doUpdates ();
					platform->endPlugModal ();
				}
			}	
			break;

		case kModalTest2Tag:
		{
			FInstancePtr <IPlatform> platform (hostClasses);
			if (platform)
				platform->beginPlugModal (STR ("Wait for ever..."));
		}	break;

		case kDeviceTestTag:
			if (p->getValueInt () > 0)
			{
				deviceTest ();
			}
			break;

		case kUndoTestTag:
			if (p->getValueInt () > 0)
				undoTest ();
			break;

		case kColorTestTag:
			if (p->getValueInt () > 0)
				colorTest ();
			break;

		case kDeviceAutomationTestTag:
			if (p->getValueInt () > 0)
				automationTest ();
			break;

		case kCloneableTestTag:
			if (p->getValueInt () > 0)
				cloneableTest ();
			break;

		case kLoadAsioDriverTag:
			if (p->getValueInt () > 0)
				loadAsioDriver ();
			break;

		case kSetMidiPortNamesTag:
			if (p->getValueInt () > 0)
				setMidiPortNames ();
			break;

		case kHideMidiPortsTag:
			if (p->getValueInt () > 0)
				hideMidiPorts ();
			break;

		case kSetupAudioPortsTag:
			if (p->getValueInt () > 0)
				setupAudioPorts ();
			break;

		case kCreateOutputChannelTag:
			if (p->getValueInt () > 0)
				createOutputChannel ();
			break;

		case kConnectAudioChannelsTag:
			if (p->getValueInt () > 0)
				connectAudioChannels ();
			break;

		case kConnectAudioSendsTag:
			if (p->getValueInt () > 0)
				connectAudioSends ();
			break;

		case kCheckAudioPortsTag :
			if (p->getValueInt () > 0)
				checkPortsOfAudioChannels ();
			break;

		case kMonitorDependecyTag:
			if (p->getValueInt () > 0)
			{
				// in this example the idle is used to create a dependency to the first monitor value
				if (monitorValue == 0)
					makeMonitorDependency ();
				else
					removeMonitorDependency (true);
			}
			break;

		case kConnectMidiChannelsTag:
			if (p->getValueInt () > 0)
				connectMidiChannels ();
			break;

		case kCreateGroupTrackTag:
			if (p->getValueInt () > 0)
				createGroupTrack ();
			break;

		case kCreateHugeBusTag:
			if (p->getValueInt () > 0)
				createHugeBus ();
			break;
	}

	return kResultFalse;
}

//------------------------------------------------------------------------------
void SKIDialogController::projectZoomTest ()
{
	if (!projectInfo)
		return;

	IProject* project = projectInfo->getActiveProject ();
	if (project)
	{
		IWindow* window =  project->getProjectWindow (); 	
		if (window)
		{
			IValue* valueLeft = window->createParamInterface ("leftTime");
			IValue* valueRight = window->createParamInterface ("rightTime");
			FReleaser valLeftRel (valueLeft);
			FReleaser valRightRel (valueRight);
		
			FUnknownPtr <ITimeValue> timeLeft (valueLeft);
			FUnknownPtr <ITimeValue> timeRight (valueRight);
			if (timeLeft && timeRight)
			{
				double diff = timeRight->getTime () - timeLeft->getTime ();
				timeRight->setTime (timeLeft->getTime () + diff*2, true);
			}
		}
	}
}

//------------------------------------------------------------------------------
void SKIDialogController::projectTest1 ()
{
	if (!projectInfo)
		return;

	IProject* project = projectInfo->getActiveProject ();
	if (!project)
		return;
	
	IMediaPool* pool = project->getMediaPool ();
	FInstancePtr <ITransportDevice> tDevice (hostClasses);

	if (pool->countMediaItems (kAudioObject) > 0)
	{
		IProjectEdit* command = HOST_NEW (IProjectEdit);
		FReleaser manipRel (command);
		if (!command)
			return;

		// get first clip from pool
		IMedium* clip = pool->getMediumByIndex (0, kAudioObject);
		FUnknownPtr <IAudioClip> audioClip (clip);
		IAudioStream* audioStream = audioClip->getIAudioStream ();

		// create an audio track
		ITrack* track = project->createTrack (kAudioObject);
		
		// init streamcount of track (mono/stereo)
		FUnknownPtr <IAudioTrack> audioTrack (track);
		if (audioTrack && audioClip)
			audioTrack->initializeStreamCount (audioStream->getChannels ());
		
		// get a basic edit context
		IProjectContext* context = project->createContext ();
		FReleaser contextDel (context);
		if (context)
		{			
			command->setEditMode (IProjectEdit::kImmediateMode);
			command->insertObject (context, track);
			command->finish (project, STR ("Add Audio Track"));

			command->setEditMode (IProjectEdit::kInitializeMode);

			IProjectContext* trackContext = project->createContext (track);
			FReleaser contextDel2 (trackContext);

			if (trackContext)
			{
				if (audioClip)
				{
					double pos = tDevice->getPosition ();
					IAudioEvent* ae = HOST_NEW (IAudioEvent);
					if (ae)
					{
						FUnknownPtr <IProjectObject> audioObj (ae);
					
						audioObj->setStartPosition (trackContext, pos);
						double length = ((double)audioStream->getFrameCount ()) / project->getNominalSampleRate ();
						audioObj->setDuration (trackContext, length);
				
						ae->setMedium (trackContext, audioClip);
						
						command->insertObject (trackContext, ae);
						pos += length;
					}

					IAudioPart* ap = HOST_NEW (IAudioPart);
					if (ap)
					{
						ap->initialize (STR ("Test part"), track);
						FUnknownPtr <IProjectObject> audioObj (ap);
						double length = ((double)audioStream->getFrameCount ()) / project->getNominalSampleRate ();
						audioObj->setStartPosition (trackContext, pos);
						audioObj->setDuration (trackContext, length);
						command->insertObject (trackContext, ap);

						IProjectContext* partContext = trackContext->createSubContext (ap);
						if (partContext)
						{
							ae = HOST_NEW (IAudioEvent);

							FUnknownPtr <IProjectObject> audioObj (ap);
							double length = ((double)audioStream->getFrameCount ()) / project->getNominalSampleRate ();
							
							audioObj->setDuration (partContext, length);
							ae->setDescription (partContext, STR ("Super Hier"));
							ae->setMedium (partContext, audioClip);

							command->insertObject (partContext, ae);
							partContext->release ();
						}
					}
				}
			}
		}
	}
	else
	{
		FInstancePtr <IAlert> alert (hostClasses);
		alert->warn (STR ("This only works if an audioclip is in the pool"));
	}
}


//------------------------------------------------------------------------------
void SKIDialogController::projectTest2 ()
{
	if (!projectInfo)
		return;

	// marker and transport
	IProject* project = projectInfo->getActiveProject ();
	if (!project)
		return;
	
	IProjectEdit* command = HOST_NEW (IProjectEdit);
	FReleaser manipRel (command);
	if (!command)
		return;

	IMarkerTrack* markerTrack = project->getMarkerTrack ();

	FUnknownPtr <IProjectObject> markerTrackObj (markerTrack);
	if (markerTrackObj && markerTrackObj->getParentObject () == 0)
	{		
		IProjectContext* context = project->createContext ();
		FReleaser contextDel (context);
		if (context)
		{			
			command->insertObject (context, markerTrack);		
			command->finish (project, STR ("Add Marker Track"));
		}
		return;
	}
	else
	{
		if (markerTrackObj)
		{
			IProjectContext* context = project->createContext (markerTrackObj->getParentObject ());
			FReleaser contextDel (context);
			markerTrackObj->setSelected (context, !markerTrackObj->isSelected ());
		}
		
		IProjectContext* context = project->createContext (markerTrack);
		FReleaser contextDel (context);
		
		FOREACH_PRJOBJ (markerTrack)
			if (object->isSelected ())
				object->setSelected (context, false);
		ENDFOR_PRJOBJ

		IMarkerObject* marker = HOST_NEW (IMarkerObject);
		FUnknownPtr <IProjectObject> markerObject (marker);

		FInstancePtr <ITransportDevice> tDevice (hostClasses);

		if (marker)
		{
			markerObject->setTitle (context, STR ("HELLO WORLD Marker"));
			markerObject->setStartPosition (context, tDevice->getPosition ());
			markerObject->setSelected (context, true);

			command->insertObject (context, marker);
			command->finish (project, STR ("Add A super hello world Marker"));

			tDevice->setPosition (tDevice->getPosition () + 10);
		}	
	}
}

//------------------------------------------------------------------------------
static IProjectObject* findFirstNonTrack (IProjectObject* object)
{
	if (object == 0)
		return 0;
	IProjectIterator* iter = object->createIterator ();
	FReleaser iterRel (iter);
	if (!iter)
		return false;

	while (iter->done () == false)
	{
		IProjectObject* subObject = iter->getNextObject ();
		if (subObject && subObject->isObjectType (kTrackObject) == false)
			return subObject;
		IProjectObject* recurseResult = findFirstNonTrack (subObject);
		if (recurseResult)
			return recurseResult;
	}
	return 0;
}

//------------------------------------------------------------------------------
void SKIDialogController::projectSelectionTest ()
{
	IProject* project = projectInfo->getActiveProject ();
	if (!project)
		return;

	FUnknownPtr<IProjectObject> po (project);
	IProjectObject*	toSelect = findFirstNonTrack (po);
	if (toSelect)
	{
		IProjectContext* c = project->createContext (toSelect);
		FReleaser cr (c);
		toSelect->setSelected (c, !toSelect->isSelected ());
	}
}


//------------------------------------------------------------------------------
void SKIDialogController::undoTest ()
{
	IProject* project = projectInfo->getActiveProject ();
	if (!project)
		return;
	
	IProjectEdit2* command = HOST_NEW (IProjectEdit2);
	FReleaser manipRel (command);
	if (!command)
		return;
	
	TestEditStep* step = new TestEditStep (hostClasses);
	command->addEditStep (step);
	step->release ();

	command->finish (project, STR ("Test Edit Step"));
}

//------------------------------------------------------------------------------
static void setColorRecursive (IProjectContext* context, 
							   IProjectEdit* command, UColorSpec color)
{
	IProjectObject* object = context->getContextObject ();
	if (object == 0)
		return;

	IProjectIterator* iter = object->createIterator ();
	FReleaser iterRel (iter);
	if (!iter)
		return;
	
	while (iter->done () == false)
	{
		IProjectObject* subObject = iter->getNextObject ();
		if (subObject)
		{	
			FUnknownPtr<IProjectObject2> p2 (subObject);
			if (p2 && p2->isObjectType (kTrackObject) == false) // do not color tracks, only events
			{
				bool hasColor = false;
				UColorSpec oldColor = 0;
				if (p2->getColor (context, oldColor) == kResultTrue)
					if (oldColor == color)
						hasColor = true;

				if (hasColor == false)
					p2->setColor (context, color, command);
			}
			
			bool isMidiPart = subObject->isObjectType (kMIDIObject) && subObject->isObjectType (kPartObject);
			// midi notes never display their own color, only the part color (or velocity or ...)
			// so do not recurse into midi parts
			if (isMidiPart == false)
			{
				// now recurse
				IProjectContext* subContext = context->createSubContext (subObject);
				FReleaser subCRel (subContext);
				if (subContext)
					setColorRecursive (subContext, command, color);
			}
		}
	}
}

//------------------------------------------------------------------------------
void SKIDialogController::colorTest ()
{
	// all events go red
	IProject* project = projectInfo->getActiveProject ();
	if (!project)
		return;

	IProjectEdit* command = HOST_NEW (IProjectEdit);
	FReleaser manipRel (command);
	if (!command)
		return;

	UColorSpec red = MakeColorSpec (255,0,0);

	IProjectContext* context = project->createContext ();
	FReleaser cr (context);
	setColorRecursive (context, command, red);
	
	command->finish (project, STR ("All Events go Red"));
}


//------------------------------------------------------------------------------
void SKIDialogController::deviceTest ()
{
	FInstancePtr <IDeviceList> deviceList (hostClasses);
	if (!deviceList)
		return;
	
	FInstancePtr <IAlert> alert (hostClasses);
	String buffer;
	
	int32 deviceCount = deviceList->countDevices ();
	for (int i = 0; i < deviceCount; i++)
	{
		IDevice* device = deviceList->getDeviceByIndex (i);
		if (device)
		{
			buffer.printf (STR ("Device %d : %s - %d parameters"), i+1, device->getTitle (), device->countParameters ());
			int32 res = alert->warn (buffer, STR ("next"), STR ("cancel"));
			if (res == 2)
				break;
		}
	}
}

//------------------------------------------------------------------------------
ITrack* SKIDialogController::getFirstAudioTrack ()
{
	IProject* project = projectInfo->getActiveProject ();
	if (!project)
		return 0;

	FUnknownPtr<IProjectObject> object (project);
	if (!object)
		return 0;

	ITrack* track = 0;
	
	{ // scope for the iterator, otherwise we can not insert a new track...
		IProjectIterator* iter = object->createIterator ();
		FReleaser iterRel (iter);
		if (!iter)
			return 0;

		while (iter->done () == false && track == 0)
		{
			IProjectObject* subObject = iter->getNextObject ();
			if (subObject->isObjectType (kTrackObject) && 
				subObject->isObjectType (kAudioObject))
			{	
				track = FUnknownPtr<ITrack> (subObject);
			}
		}
	}
	if (track == 0)
	{
		track = project->createTrack (kAudioObject);

		IProjectContext* context = project->createContext ();
		FReleaser contextDel (context);

		IProjectEdit* command = HOST_NEW (IProjectEdit);
		FReleaser commandRel (command);
		if (command && context)
		{
			command->setEditMode (IProjectEdit::kInitializeMode);
			command->insertObject (context, track);
		}
	}
	return track;
}

//------------------------------------------------------------------------------
void SKIDialogController::automationTest ()
{
	// we need an audio track for it:
	ITrack* track = getFirstAudioTrack ();
	
	TestDeviceNode* nodeDef = 0;
	IAutomation2* automation = 0;
	bool createCurve = true;

	if (track)
	{
		automation = FUnknownPtr<IAutomation2> (track->getAutomation ());
		if (automation)
		{
			ISkiAutomationNode* myPrivateNode = automation->getPrivateNode (TestDeviceNode::classID);
			if (myPrivateNode == 0)				
				myPrivateNode = automation->createPrivateNode (TestDeviceNode::classID);
			
			if (myPrivateNode && myPrivateNode->getDefinition ())
			{
				nodeDef = (TestDeviceNode*) myPrivateNode->getDefinition ();
				IAutomationTrack* track = automation->getPrivateTrack (TestDeviceNode::classID, TestDeviceNode::kIDParam1);
				FUnknownPtr<IProjectObject> tackObj (track);
				if (tackObj)
				{
					IProjectIterator* iter = tackObj->createIterator ();
					FReleaser iterRel (iter);
					if (iter && iter->countObjects () > 0)
					{
						createCurve = false;
					}				
				}
			}
			automation->enableRead (true);
			automation->enableWrite (true); // must be enabled, otherwise writeParameterTest can not write
		}
	}

	if (nodeDef)
	{
		IValue* v = values.getValue ("testParam");
		nodeDef->setParam1Value (v);
	
		if (createCurve)
			nodeDef->writeParameterTest ();
		
		if (automation)
			automation->expand (true);
	}
}

//------------------------------------------------------------------------------
void SKIDialogController::cloneableTest ()
{
	// demonstrate how a plugin object can be attched to a host project object
	// and what happens when the host object is copied

	IProject* project = projectInfo->getActiveProject ();
	if (!project)
		return;

	// first we need a track:
	FUnknownPtr<IProjectObject> trackObj (getFirstAudioTrack ());
	if (trackObj)
	{
		// we need something on the track
		IProjectObject* object = 0;
		
		IProjectIterator* iter = trackObj->createIterator ();
		if (iter)
		{
			object = iter->getNextObject ();
			iter->release ();
		}

		if (object == 0)
		{
			IProjectEdit* command = HOST_NEW (IProjectEdit);
			FReleaser manipRel (command);
			if (command)
			{
				IProjectContext* trackContext = project->createContext (trackObj);
				FReleaser contextDel2 (trackContext);
				
				if (trackContext)
				{
					IAudioPart* audioPart = HOST_NEW (IAudioPart);
					FUnknownPtr <IProjectObject> audioObj (audioPart);
				
					if (audioObj && audioPart)
					{
						audioPart->initialize (STR ("Test part"), trackObj);

						audioObj->setStartPosition (trackContext, 10.0);
						audioObj->setDuration (trackContext, 10.0);
						
						audioObj->setUserAttribute ("test", 10.0, true); // only used for host persistence testing...
						
						command->insertObject (trackContext, audioObj);
						command->finish (project, STR ("Add Part"));
						object = audioObj;
					}
				}
			}
		}

		if (object)
		{
			// now install a cloneable object. the object is not persistent (this would require IPersistent)
			// it is only copied instead of referenced when the project object is copied. 
			
			object->setUserAttribute ("clon", new SkiCloneable (hostClasses), false);
		
			// create a copy to demonstrate:
			IProjectObject* theCopy = object->createCopy (IProjectObject::kShared);
			
			FReleaser copyRel (theCopy);
		}
	}
}


//------------------------------------------------------------------------------
void SKIDialogController::loadAsioDriver ()
{
	// here you can load your asio driver
	FInstancePtr<IAudioDeviceManager> audioDeviceManager (hostClasses);
	if (audioDeviceManager)
	{
		for (int32 i = 0; i < audioDeviceManager->countRegisteredDrivers (); i++)
		{
			const tchar* driverName = audioDeviceManager->getAsioDriverName (i);
			if (driverName && tstrcmp (driverName, STR ("ASIO Multimedia Driver")) == 0)
			{
				bool loaded = audioDeviceManager->installAudioDevice (driverName, false) == kResultTrue; 
				
				FInstancePtr<IAlert> alert (hostClasses);
				if (alert)
				{
					if (loaded)
						alert->warn (STR ("Asio Driver successfully loaded!"));
					else
						alert->warn (STR ("Error: Asio Driver failed to load!"));
				}
				
				break;
			}
		}
	}
}

//------------------------------------------------------------------------------
// example how to set the display name of midi ports / to hide ports
void SKIDialogController::setMidiPortNames ()
{
	FInstancePtr<IPortRegistry> portRegistry (hostClasses);
	if (portRegistry)
	{
		for (int32 i = 0; i < portRegistry->countPorts (); i++)
		{
			IPort* port = portRegistry->getPortByIndex (i);
			if (port)
			{
				if (port->isPortType (kMidiPortType) && 
					port->isSubType (kSystemPortType))
				{
					if (tstrcmp (port->getSysName (), STR ("Yahama WLAN Port 1")) == 0)
					{
						// rename port
						if (port->isSystemInput ())
							port->setDisplayName (STR ("DX 7 In"));
						else
							port->setDisplayName (STR ("DX 7 Out"));
					}
					else
					{
						// hide port
						if (port->isSystemInput ())
							port->setDisplayName (STR ("This is a input port :)"));
						else
							port->setDisplayName (STR ("This is a output port :)"));
					}
				}
			}
		}
	}
}

//------------------------------------------------------------------------------
// example how to hide midi ports
void SKIDialogController::hideMidiPorts ()
{
	FInstancePtr<IPortRegistry> portRegistry (hostClasses);
	if (portRegistry)
	{
		for (int32 i = 0; i < portRegistry->countPorts (); i++)
		{
			IPort* port = portRegistry->getPortByIndex (i);
			if (port)
			{
				if (port->isPortType (kMidiPortType) && 
					port->isSubType (kSystemPortType))
				{
					port->setVisible (false);		
				}
			}
		}
	}
}


//------------------------------------------------------------------------------
// example how to rename asio ports
void SKIDialogController::setupAudioPorts ()
{
	FInstancePtr<IPortRegistry> portRegistry (hostClasses);
	if (portRegistry)
	{
		for (int32 i = 0; i < portRegistry->countPorts (); i++)
		{
			IPort* port = portRegistry->getPortByIndex (i);
			if (port)
			{
				if (port->isPortType (kAudioPortType) && 
					port->isSubType (kSystemPortType))
				{
					if (tstrcmp (port->getSysName (), STR ("Yahama WLAN Audio Port 88")) == 0)
					{
						// rename port
						if (port->isSystemInput ())
							port->setDisplayName (STR ("Audio In 88"));
						else
							port->setDisplayName (STR ("Audio Out 88"));
					}
				}
			}
		}
	}
}



//------------------------------------------------------------------------------
IDevice* SKIDialogController::getVstChannelNode ()
{
	FInstancePtr<IDeviceList> deviceList (hostClasses);
	if (deviceList)
	{
		IDevice* vstMixer = deviceList->getDeviceByClassID ("VST Mixer", 0); 
		if (vstMixer)
		{
			// first find the channel node of the device
			for (int32 i = 0; i < vstMixer->countSubDevices (); i++)
			{
				IDevice* subDevice = vstMixer->getSubDevice (i);
				if (subDevice && subDevice->getDeviceClass () &&
					strcmp (IDeviceNode::kChannels, subDevice->getDeviceClass ()) == 0)
				{
					return subDevice;
				}
			}
		}
	}
	return 0;
}

//------------------------------------------------------------------------------
IDevice* SKIDialogController::getMidiChannelNode ()
{
	FInstancePtr<IDeviceList> deviceList (hostClasses);
	if (deviceList)
	{
		IDevice* midiMixer = deviceList->getDeviceByClassID ("Midi Mixer", 0); 
		if (midiMixer)
		{
			// first find the channel node of the device
			for (int32 i = 0; i < midiMixer->countSubDevices (); i++)
			{
				IDevice* subDevice = midiMixer->getSubDevice (i);
				if (subDevice && subDevice->getDeviceClass () &&
					strcmp (IDeviceNode::kChannels, subDevice->getDeviceClass ()) == 0)
				{
					return subDevice;
				}
			}
		}
	}
	return 0;
}



//------------------------------------------------------------------------------
// example how to create an stereo output channel (bus) and to connect it to audio ports
void SKIDialogController::createOutputChannel ()
{
	FUnknownPtr<Vst::IChannelManager> channelManager;
	
	FInstancePtr<IDeviceList> deviceList (hostClasses);
	if (deviceList)
		channelManager = deviceList->getDeviceByClassID ("VST Mixer", 0); 
	
	FInstancePtr<IPortRegistry> portRegistry (hostClasses);

	if (channelManager && portRegistry)
	{
		Vst::IBusDescriptor* bus = HOST_NEW (Vst::IBusDescriptor);
		FReleaser busRel (bus); // we must free the bus that we created here

		bool foundChannel = false;
		
		// here you can check for already exising io channels
		IDevice* channelNode = getVstChannelNode ();
	
		// iteratate all channels of the device and check for io channels
		if (channelNode)
		{
			for (int32 i = 0; i < channelNode->countSubDevices (); i++)
			{
				IDevice* channel = channelNode->getSubDevice (i);
				FUnknownPtr<Vst::IIOChannel> ioChannel (channel);
				if (ioChannel)
				{
					String tag = ioChannel->getTagString ();
					if (tag == "Y1")
						foundChannel = true;
					else 
					{ 
						// disconnect busses that are not yamaha busses...
						if (ioChannel->getBusDescriptor (bus) == kResultTrue)
						{
							for (int32 j = 0; j < bus->countPins (); j++)
								bus->setPinConnection (j, 0);
							ioChannel->setBusDescriptor (bus);
						}
					}
				}
			}
		}

		//-----------------------------------------
		// example here: create stereo output channel
		if (foundChannel == false)
		{
			if (bus)
			{
				bus->createPins (Vst::SpeakerArr::kStereo);

				// find audio ports and connect them
				int32 connectedPorts = 0;
				for (int32 i = 0; i < portRegistry->countPorts (); i++)
				{
					IPort* port = portRegistry->getPortByIndex (i);
					if (port)
					{
						if (port->isPortType (kAudioPortType) && 
							port->isSubType (kSystemPortType) && 
							port->isSystemInput () == false)
						{
							if (bus->setPinConnection (connectedPorts, port) == kResultTrue)
								connectedPorts++;
						}
					}

					if (connectedPorts == bus->countPins ())
						break;
				}

				// create channel and set bus descriptor 
				Vst::IIOChannel* channel = channelManager->createIOChannel (false, STR ("Yamaha Stereo Out"), "Y1", bus);
			}
		}

	}
}

//------------------------------------------------------------------------------
void SKIDialogController::createHugeBus ()
{
	FUnknownPtr<Vst::IChannelManager> channelManager;	
	FInstancePtr<IDeviceList> deviceList (hostClasses);
	if (deviceList)
		channelManager = deviceList->getDeviceByClassID ("VST Mixer", 0); 

	if (channelManager)
	{
		Vst::IBusDescriptor* bus = HOST_NEW (Vst::IBusDescriptor);
		FReleaser busRel (bus); // we must free the bus that we created here
		if (bus)
		{
			bus->createPins (0xFFFFFFFF);
			channelManager->createIOChannel (false, STR ("Huge Bus"), "Huge", bus);
		}
	}
}


//------------------------------------------------------------------------------
// example how to connect audio channel to input/output channels
void SKIDialogController::connectAudioChannels ()
{
	IDevice* channelNode = getVstChannelNode ();
	if (!channelNode)
		return;

	int32 i;
	TArray<IDevice*> audioChannels;
	Vst::IIOChannel* myOutputChannel = 0;

	// first collect all inputs and all audio channels in separate arrays
	for (i = 0; i < channelNode->countSubDevices (); i++)
	{
		IDevice* channel = channelNode->getSubDevice (i);

		FUnknownPtr<Vst::IIOChannel> ioChannel (channel);
		if (ioChannel)
		{
			if (myOutputChannel == 0 && ioChannel->isInput () == false)
			{
				String tag = ioChannel->getTagString ();
				if (tag == "Y1")
					myOutputChannel = ioChannel;
			}
		}	
		else
		{
			if (strcmp (channel->getDeviceClass (), "AudioChannel") == 0)
				audioChannels.add (channel);
		}
	}

	// this code tries to set the output of all audio channels 
	if (myOutputChannel)
	{
		for (i = 0; i < audioChannels.total (); i++)
		{
			FUnknownPtr<IConnector> connector (audioChannels.at (i));
			if (connector)
			{
				connector->connectTo (myOutputChannel);			
			}
		}
	}
}

// helper function for finding the send slot folder of a channel
//------------------------------------------------------------------------------
static IDevice* findSubDevice (IDevice* parent, FIDString id, int32 index = 0)
{
	int32 subDeviceCount = parent->countSubDevices ();
	int32 findCounter = 0;
	for (int32 i = 0; i < subDeviceCount; i++)
	{
		IDevice* subDevice = parent->getSubDevice (i);
		if (subDevice)
		{
			FIDString deviceClass = subDevice->getDeviceClass ();
			if (deviceClass && strcmp (id, deviceClass) == 0)
			{
				if (findCounter == index)
					return subDevice;
				findCounter++;
			}				
		}
	}
	return 0;
}


// example how to connect the sends of an audio channel to output channels
//------------------------------------------------------------------------------
void SKIDialogController::connectAudioSends ()
{
	IDevice* channelNode = getVstChannelNode ();
	if (!channelNode)
		return;

	int32 i;
	TArray<IDevice*> audioChannels;
	Vst::IIOChannel* destination = 0;

	// again: first collect all inputs and all audio channels in separate arrays
	for (i = 0; i < channelNode->countSubDevices (); i++)
	{
		IDevice* channel = channelNode->getSubDevice (i);

		FUnknownPtr<Vst::IIOChannel> ioChannel (channel);
		if (ioChannel)
		{
			if (destination == 0 && ioChannel->isInput () == false)
				destination = ioChannel;
		}	
		else
		{
			if (strcmp (channel->getDeviceClass (), "AudioChannel") == 0)
				audioChannels.add (channel);
		}
	}

	// this code tries to set the output of all audio channels 
	if (destination)
	{
		for (i = 0; i < audioChannels.total (); i++)
		{
			IDevice* sendFolder = findSubDevice (audioChannels.at (i), "Sends");
			if (sendFolder)
			{
				IDevice* slot1 = sendFolder->getSubDevice (0);
				if (slot1)
				{
					FUnknownPtr<IConnector> connector (slot1);
					if (connector)
					{
						connector->connectTo (destination);			

						IValue* volumeValue = slot1->createParamInterfaceByID ("volume");
						FReleaser vvr (volumeValue);
						if (volumeValue)
							volumeValue->setNormalized (0.8f, true);

						IValue* onValue = slot1->createParamInterfaceByID ("on");
						FReleaser ovr (onValue);
						if (onValue)
							onValue->setValue2 (1, true);
					}				
				}
			}			
		}
	}
}


//------------------------------------------------------------------------------
void SKIDialogController::connectMidiChannels ()
{
	IDevice* channelNode = getMidiChannelNode ();
	if (!channelNode)
		return;

	IPort* input = 0;
	IPort* output = 0;
	int32 i;

	FInstancePtr<IPortRegistry> portRegistry (hostClasses);
	if (portRegistry)
	{
		for (i = 0; i < portRegistry->countPorts (); i++)
		{
			IPort* port = portRegistry->getPortByIndex (i);
			if (port && port->isPortType (kMidiPortType) && port->isSubType (kSystemPortType))
			{
				if (port->isSystemInput ())
				{
					if (input == 0)
						input = port;
				}
				else
				{
					if (output == 0)
						output = port;
				}
				if (input && output)
					break;
			}
		}
	}


	for (i = 0; i < channelNode->countSubDevices (); i++)
	{
		IDevice* channel = channelNode->getSubDevice (i);

		FUnknownPtr<IConnector> connector (channel);
		if (connector)
		{
			connector->connectTo (input);	
			connector->connectTo (output);	
		}
	}
}


//------------------------------------------------------------------------------
// example here: check which audio ports are assinged to Audio channel 
void SKIDialogController::checkPortsOfAudioChannels ()
{
	IDevice* channelNode = getVstChannelNode ();
	if (!channelNode )
		return;
	
	int32 i;
	
	// first collect all audio channels and all ioChannels
	TArray<IDevice*> audioChannels;
	TArray<IDevice*> ioChannels;
	
	for (i = 0; i < channelNode->countSubDevices (); i++)
	{
		IDevice* channel = channelNode->getSubDevice (i);
		FUnknownPtr<Vst::IIOChannel> ioChannel (channel);
		if (ioChannel)
			ioChannels.add (channel);
		else 
		{
			const char* deviceClass = channel->getDeviceClass ();
			if (deviceClass && strcmp (deviceClass, "AudioChannel") == 0) // Audio Track ?
				audioChannels.add (channel);
		}
	}

	bool lookupInputs = true;

	// then iterate through the audio channels
	for (i = 0; i < audioChannels.total (); i++)		
	{
		IDevice* audioChannel = audioChannels.at (i);
		FUnknownPtr<IConnector> connector (audioChannel);
		
		// use the connector interface of the audio channel and find the iochannel that is
		// connected to the audio channel
		if (connector)
		{
			// iterate the ioChannels
			for (int32 j = 0; j < ioChannels.total (); j++)		
			{
				IDevice* ioChannel = ioChannels.at (j);
				FUnknownPtr<Vst::IIOChannel> vstIOChannel (ioChannel);
				
				// here it depends if you look for inputs or outputs
				if (lookupInputs == vstIOChannel->isInput ())
				{
					if (connector->isConnected (ioChannel))
					{
						// the current ioChannels is connected to the audio channel

						Vst::IBusDescriptor* bus = HOST_NEW (Vst::IBusDescriptor);
						FReleaser busRel (bus); 
						if (vstIOChannel->getBusDescriptor (bus) == kResultOk)
						{
							int32 pinCount  = bus->countPins();
							for (int32 pin = 0; pin < pinCount; pin++) 
							{
								IPort* port = bus->getPinConnection (pin);
								if (port)
								{
									if (tstrcmp (port->getSysName (), STR ("mLAN 01")) == 0)
										break;
								}
							}
						}
					}
				}
			}
		}
	}
}

//------------------------------------------------------------------------------
void SKIDialogController::makeMonitorDependency ()
{
	if (monitorValue)
		return;

	IDevice* channelNode = getVstChannelNode ();
	if (!channelNode)
		return;
	
	// first collect all inputs and all audio channels in separate arrays
	for (int32 i = 0; i < channelNode->countSubDevices (); i++)
	{
		IDevice* channel = channelNode->getSubDevice (i);
		if (channel)
		{
			monitorValue = channel->createParamInterfaceByID (kParamInputMonitor); 
			if (monitorValue)
			{
				FInstancePtr<IUpdateHandler> updateHandler (hostClasses);
				if (updateHandler)
				{
					updateHandler->addDependent (monitorValue, this);
	
					FInstancePtr<IAlert> alert (hostClasses);
					if (alert)
						alert->warn (STR ("Dependecy to monitor value installed!"));
				}
				else
				{
					monitorValue->release ();
					monitorValue = 0;
				}
				return;
			}
		}
	}

	FInstancePtr<IAlert> alert (hostClasses);
	if (alert)
		alert->warn (STR ("No Channel found!"));
}

//------------------------------------------------------------------------------
void SKIDialogController::removeMonitorDependency (bool release)
{
	if (monitorValue)
	{
		FInstancePtr<IUpdateHandler> updateHandler (hostClasses);
		if (updateHandler)
			updateHandler->removeDependent (monitorValue, this);
		if (release)
			monitorValue->release ();
		monitorValue = 0;
	}
}

//------------------------------------------------------------------------------
void PLUGIN_API SKIDialogController::update (FUnknown* changedUnknown, int32 message)
{
	FUnknownPtr<IValue> value (changedUnknown);
	if (value && value == monitorValue)
	{
		if (message == kDestroyed)	
		{
			removeMonitorDependency (false);
		}
		else
		{
			FInstancePtr<IAlert> alert (hostClasses);
			if (alert)
			{				
				if (value->getValueInt () > 0)
					alert->warn (STR ("You have switched on the monitor button :-)"));
				else
					alert->warn (STR ("You have switched off the monitor button :-("));
			}	
		}
	}
}

//------------------------------------------------------------------------------
void SKIDialogController::createGroupTrack ()
{
	if (!projectInfo)
		return;

	IProject* project = projectInfo->getActiveProject ();
	if (!project)
		return;

	IProjectEdit* command = HOST_NEW (IProjectEdit);
	FReleaser manipRel (command);
	if (!command)
		return;

	ITrack* track = project->createTrack (kGroupTrackObject);

	FUnknownPtr <IAudioTrack> audioTrack (track);
	if (audioTrack)
		audioTrack->initializeStreamCount (6);// 5.1

	IProjectContext* context = project->createContext ();
	FReleaser contextDel (context);
	if (context)
	{			
		command->setEditMode (IProjectEdit::kBulkMode);
		command->insertObject (context, track);
		command->finish (project, STR ("Add Group Track"));
	}
}


//------------------------------------------------------------------------------
//------------------------------------------------------------------------------
//------------------------------------------------------------------------------

#if WINDOWS

#include <windows.h>
class TestPlatformView  : public CPluginView
{
public:
	TestPlatformView (ViewRect* rect);
	~TestPlatformView ();

	tresult PLUGIN_API attached (void* parent);
	tresult PLUGIN_API removed ();

	tresult PLUGIN_API idle ();
	tresult PLUGIN_API onWheel (float distance) { return kResultFalse; }
	tresult PLUGIN_API onKey (char8 asciiKey, int32 keyMsg, int32 modifiers);
	tresult PLUGIN_API onSize (ViewRect* newSize);

	tresult PLUGIN_API onFocus (TBool state);

	void onMouse (UINT message, WPARAM wParam, LPARAM lParam);
	bool paint (UINT message, WPARAM wParam, LPARAM lParam);

protected:
	HWND hWnd;
	COLORREF color;
	bool hasFocus;
	bool inputting;
	
	enum {kMaxText = 128};
	tchar text [kMaxText];

	void setColor (COLORREF c);

	static LRESULT CALLBACK WndProc (HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam);
};


//------------------------------------------------------------------------------
LRESULT CALLBACK TestPlatformView::WndProc (HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam)
{
	TestPlatformView* view = (TestPlatformView*)::GetWindowLongPtr (hWnd, GWLP_USERDATA);
	
	switch (message)
	{
		case WM_ERASEBKGND :
		case WM_PAINT:
			if (view)
			{
				if (view->paint (message, wParam, lParam))
					return 0;
			}
			return DefWindowProc (hWnd, message, wParam, lParam);
		case WM_LBUTTONDOWN:
		case WM_LBUTTONUP:
		case WM_MOUSEMOVE:
		case WM_LBUTTONDBLCLK:
			if (view)
				view->onMouse (message, wParam, lParam);
			break;
		default:
			return DefWindowProc (hWnd, message, wParam, lParam);
	}
	return 0;
}

//------------------------------------------------------------------------------
TestPlatformView::TestPlatformView (ViewRect* rect)
: CPluginView (rect)
, hWnd (0)
, hasFocus (false)
, inputting (false)
{
	color = RGB (20,20,80);
	memset (text, 0 , sizeof (text));
}

//------------------------------------------------------------------------------
TestPlatformView::~TestPlatformView ()
{
}

//------------------------------------------------------------------------------
tresult PLUGIN_API TestPlatformView::attached (void* parent)
{	
	extern void* moduleHandle; // defined in dllmain.cpp
	HINSTANCE hInstance = (HINSTANCE)moduleHandle;
	const tchar* kClassName = TEXT ("TestChildWindow");

	static bool registered = false;
	if (!registered)
	{
		WNDCLASS wndClass = {0};
		wndClass.hbrBackground = (HBRUSH)GetStockObject(WHITE_BRUSH);
		wndClass.hCursor = LoadCursor(NULL, IDC_ARROW);
		wndClass.hIcon = NULL;
		wndClass.hInstance = hInstance;
		wndClass.lpfnWndProc = WndProc;
		wndClass.lpszClassName = kClassName;
		wndClass.style = CS_HREDRAW | CS_VREDRAW | CS_DBLCLKS;
		
		if (RegisterClass(&wndClass))
			registered = true;
	}
		        
	hWnd = CreateWindow (kClassName, TEXT ("test"), WS_CHILD | WS_VISIBLE,
			rect.left, rect.top, rect.right-rect.left , rect.bottom-rect.top,
            (HWND) parent, NULL, hInstance, NULL);

	if (hWnd)
		SetWindowLongPtr (hWnd, GWLP_USERDATA, (LONG_PTR) this);

	return CPluginView::attached (parent);
}

//------------------------------------------------------------------------------
tresult PLUGIN_API TestPlatformView::removed ()
{
	if (hWnd)
		DestroyWindow (hWnd);

	hWnd = 0;
	return CPluginView::removed ();
}

//------------------------------------------------------------------------------
void TestPlatformView::setColor (COLORREF c)
{
	color = c;
	InvalidateRect (hWnd, 0, false);
	UpdateWindow (hWnd);
}

//------------------------------------------------------------------------------
tresult PLUGIN_API TestPlatformView::onFocus (TBool state)
{
	hasFocus = state != 0;
	InvalidateRect (hWnd, 0, true);
	UpdateWindow (hWnd);
	return kResultTrue;
}

//------------------------------------------------------------------------------
tresult PLUGIN_API TestPlatformView::idle ()
{
	return kResultTrue;
}

//------------------------------------------------------------------------------
tresult PLUGIN_API TestPlatformView::onKey (char8 character, int32 vKey, int32 modifiers)
{
	if (inputting)
	{
		bool needRedraw = false;
		if (vKey == KEY_RETURN)
		{
			inputting = false;
			needRedraw = true;
		}
		else if (vKey == KEY_DELETE)
		{
			text [0] = 0;
			needRedraw = true;
		}
		else if (vKey == KEY_BACK)
		{
			long textLen = tstrlen (text);
			if (textLen > 0)
				text [textLen-1] = 0;
			needRedraw = true;
		}
		else if (character != 0)
		{
			long textLen = tstrlen (text);
			if (textLen < kMaxText-1)
			{
				text [textLen] = character;
				text [textLen+1] = 0;
			}
			else
			{
				text [0] = character;
				text [1] = 0;
			}
			needRedraw = true;
		}
	
		if (needRedraw)
		{
			InvalidateRect (hWnd, 0, false);
			UpdateWindow (hWnd);		
		}
		return kResultTrue;
	}
	return kResultFalse;
}

//------------------------------------------------------------------------------
tresult PLUGIN_API TestPlatformView::onSize (ViewRect* newSize)
{
	if (hWnd)
	{
		SetWindowPos (hWnd, 0, newSize->left, newSize->top, newSize->right - newSize->left,
			newSize->bottom - newSize->top, SWP_NOREPOSITION);
	}

	return CPluginView::onSize (newSize);
}

//------------------------------------------------------------------------------
void TestPlatformView::onMouse (UINT message, WPARAM wParam, LPARAM lParam)
{
	if (message == WM_LBUTTONDOWN)
	{
		setColor (color + LOWORD(lParam) + HIWORD(lParam));
	}
	else if (message == WM_LBUTTONDBLCLK)
	{
		inputting = !inputting;
		InvalidateRect (hWnd, 0, false);
		UpdateWindow (hWnd);		
	}
}

//------------------------------------------------------------------------------
bool TestPlatformView::paint (UINT message, WPARAM wParam, LPARAM lParam)
{
	RECT r;
	if (message == WM_ERASEBKGND)
	{
		GetClientRect (hWnd, &r);
		HBRUSH backBrush;
		if (hasFocus)
			backBrush = (HBRUSH)GetStockObject (BLACK_BRUSH);
		else
			backBrush = (HBRUSH)GetStockObject (WHITE_BRUSH);
		
		FillRect ((HDC)wParam, &r, backBrush);	
		return true;
	}
	
	PAINTSTRUCT ps;
	BeginPaint (hWnd, &ps);

	long offset = 20;
	SetRect (&r, rect.left + offset, rect.top + offset, rect.right - offset, rect.bottom - offset);	
	if (r.right > r.left && r.bottom > r.top)
	{
		LOGBRUSH logBrush = { BS_SOLID, color, 0 };
		HBRUSH newBrush = CreateBrushIndirect (&logBrush);
		SelectObject (ps.hdc, newBrush);
		SelectObject (ps.hdc, GetStockObject (NULL_PEN));
		
		RoundRect (ps.hdc, r.left, r.top, r.right, r.bottom, offset, offset);	

		SelectObject (ps.hdc, GetStockObject (NULL_BRUSH));
		DeleteObject (newBrush);

		if (inputting)
		{
			RECT r2;
			SetRect (&r2, r.left + offset, r.top + offset, r.right - offset, r.top + offset * 2);	

			logBrush.lbColor = RGB (230, 220, 0);
			newBrush = CreateBrushIndirect (&logBrush);
			FillRect (ps.hdc, &r2, newBrush);	
			DeleteObject (newBrush);
		
		}
		
		long textLen = tstrlen (text);
		if (textLen > 0)
		{
			SelectObject (ps.hdc, GetStockObject (ANSI_VAR_FONT));
			TextOut (ps.hdc, r.left + offset, r.top + offset, text, textLen);	
		}
	}

	EndPaint (hWnd, &ps);
	return true;
}

#endif


//------------------------------------------------------------------------------
//------------------------------------------------------------------------------
SKITestViewController::SKITestViewController ()
{
	FUNKNOWN_CTOR
}

//------------------------------------------------------------------------------
SKITestViewController::~SKITestViewController ()
{	
	FUNKNOWN_DTOR
}

//------------------------------------------------------------------------------
IMPLEMENT_REFCOUNT (SKITestViewController)
tresult PLUGIN_API SKITestViewController::queryInterface (const char* iid, void** obj)
{
	QUERY_INTERFACE (iid, obj, ::FUnknown::iid, IPlugController)
	QUERY_INTERFACE (iid, obj, IPlugController::iid, IPlugController)
	QUERY_INTERFACE (iid, obj, IViewBuilder::iid, IViewBuilder)
	QUERY_INTERFACE (iid, obj, IMessageReceiver::iid, IMessageReceiver)
	
    *obj = 0;
    return kNoInterface;
}

//------------------------------------------------------------------------------
tresult PLUGIN_API SKITestViewController::createView (const char* name, ViewRect* rect, FUnknown** view /*out*/)
{
#if WINDOWS
	if (strcmp (name, "TestView") == 0)
	{
		*view = (IPlugView*)new TestPlatformView (rect);
		return kResultTrue;
	}
#endif
	return kNotImplemented;	
}

//------------------------------------------------------------------------------
tresult PLUGIN_API SKITestViewController::notifyMessage (IMessage* message)
{
	if (message->hasMessageID ("Selection Changed"))
	{
		int64 tmp = 0;
		if (message->getInt ("IsProjectSelection", &tmp) == kResultTrue 
			&& tmp > 0)
		{
		
			return kResultTrue;
		}
	}
	return kResultFalse;
}
