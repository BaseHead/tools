
#define INIT_CLASS_IID // this will define all class ids

#include "main/pluginfactory.h"
#include "skicomponent.h"
#include "skiexampledialog.h"

#include "pluginterfaces/base/istringresult.h"
#include "pluginterfaces/host/project/iaudioobjects.h"
#include "pluginterfaces/host/devices/idevice.h"
#include "pluginterfaces/host/ihostclasses.h"
#include "pluginterfaces/host/project/imarkertrack.h"
#include "pluginterfaces/host/project/iprojectedit.h"
#include "pluginterfaces/host/project/iautomationobjects.h"  
#include "pluginterfaces/host/devices/itransportdevice.h"
#include "pluginterfaces/host/devices/itimevalue.h"
#include "pluginterfaces/gui/iplugcontroller.h"
#include "public.sdk/source/common/pluginview_old.h"
#include "pluginterfaces/gui/iplugview.h"

//------------------------------------------------------------------------------
// Steinberg plug-in entrypoint
//------------------------------------------------------------------------------
BEGIN_FACTORY ("BaseHead, LLC", 
			   "http://www.baseheadinc.com", 
			   "mailto:support@baseheadinc.com", 
			   PFactoryInfo::kNoFlags)

DEF_CLASS2 (INLINE_UID (0x0DB4050B, 0x4AE644ED, 0x937E98B1, 0x95893B3E), 
			1,    
			"Service",
			"BaseHead SKI plugin",
			0,
			0,
			"1.6.5.2",
			0,
			SKIComponent::newInstance)

DEF_CLASS1 (TestDeviceNode::classID,
			1,    
			"",
			"Basehead Test Device Node",
			TestDeviceNode::newInstance)

END_FACTORY




//------------------------------------------------------------------------------
// module init/exit
//------------------------------------------------------------------------------
bool InitModule ()   { return true; }
bool DeinitModule () { return true; }