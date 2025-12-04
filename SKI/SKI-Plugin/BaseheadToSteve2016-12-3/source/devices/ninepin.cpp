
//-----------------------------------------------------------------------------
// Project      : SKI - Devices
// Filename     : ninepin.h
// Created by   : Steinberg
// Description  : Wrapper class to nine-pin device
//
//-----------------------------------------------------------------------------
// LICENSE
// (c) 2020, Steinberg Media Technologies GmbH, All Rights Reserved
//-----------------------------------------------------------------------------
// This Software Development Kit may not be distributed in parts or its entirety  
// without prior written agreement by Steinberg Media Technologies GmbH. 
// This SDK must not be used to re-engineer or manipulate any technology used  
// in any Steinberg or Third-party application or software module, 
// unless permitted by law.
// Neither the name of the Steinberg Media Technologies nor the names of its
// contributors may be used to endorse or promote products derived from this 
// software without specific prior written permission.
// 
// THIS SDK IS PROVIDED BY STEINBERG MEDIA TECHNOLOGIES GMBH "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED 
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
// IN NO EVENT SHALL STEINBERG MEDIA TECHNOLOGIES GMBH BE LIABLE FOR ANY DIRECT, 
// INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, 
// BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, 
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF 
// LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE 
// OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED
// OF THE POSSIBILITY OF SUCH DAMAGE.
//-----------------------------------------------------------------------------

#include "ninepin.h"
#include "pluginterfaces/host/devices/idevice.h"
#include "pluginterfaces/host/ihostclasses.h"
#include "pluginterfaces/host/devices/itimevalue.h"
#include "pluginterfaces/gui/ivalue.h"

namespace Steinberg {

//-----------------------------------------------------------------------------
NinePinDevice::NinePinDevice (IHostClasses* hostClasses)
:	deviceInterface (0)
{
	FInstancePtr <IDeviceList> deviceList (hostClasses);
	if (deviceList)
	{
		deviceInterface = deviceList->getDeviceByClassID ("9-Pin Device 1", 0);
		if (deviceInterface)
			deviceInterface->addRef ();
	}
}
	
//-----------------------------------------------------------------------------
NinePinDevice::~NinePinDevice ()
{
	if (deviceInterface)
		deviceInterface->release ();
}

//-----------------------------------------------------------------------------
bool NinePinDevice::verify ()
{
	return deviceInterface != 0;
}

//-----------------------------------------------------------------------------
bool NinePinDevice::isStopped ()
{
	IValue* value = deviceInterface->createParamInterfaceByID ("stopped");
	FReleaser r (value);
	if (value)
		return value->getValue () != 0;
	return false;
}

//-----------------------------------------------------------------------------
bool NinePinDevice::isRunning ()
{
	IValue* value = deviceInterface->createParamInterfaceByID ("running");
	FReleaser r (value);
	if (value)
		return value->getValue () != 0;
	return false;
}

//-----------------------------------------------------------------------------
bool NinePinDevice::isCueing ()
{
	IValue* value = deviceInterface->createParamInterfaceByID ("cueing");
	FReleaser r (value);
	if (value)
		return value->getValue () != 0;
	return false;
}

//-----------------------------------------------------------------------------
void NinePinDevice::setOnline (bool state)
{
	IValue* value = deviceInterface->createParamInterfaceByID ("online");
	FReleaser r (value);
	if (value)
		value->setValue2 (state ? 1:0, true);
}


//-----------------------------------------------------------------------------
bool NinePinDevice::isOnline ()
{
	IValue* value = deviceInterface->createParamInterfaceByID ("online");
	FReleaser r (value);
	if (value)
		return value->getValue () != 0;
	return false;
}

//-----------------------------------------------------------------------------
double NinePinDevice::getDevicePosition ()
{
	IValue* value = deviceInterface->createParamInterfaceByID ("devicePosition");
	FReleaser r (value);
	if (value)
	{
		FUnknownPtr <ITimeValue> timeValue (value);
		if (timeValue)
			return timeValue->getTime ();	
	}
	return 0;
}

//-----------------------------------------------------------------------------
void NinePinDevice::start ()
{
	IValue* value = deviceInterface->createParamInterfaceByID ("start");
	FReleaser r (value);
	if (value)
		value->setValue2 (1, true);
}

//-----------------------------------------------------------------------------
void NinePinDevice::stop ()
{
	IValue* value = deviceInterface->createParamInterfaceByID ("stop");
	FReleaser r (value);
	if (value)
		value->setValue2 (1, true);
}

//-----------------------------------------------------------------------------
void NinePinDevice::forward ()
{
	IValue* value = deviceInterface->createParamInterfaceByID ("forward");
	FReleaser r (value);
	if (value)
		value->setValue2 (1, true);
}

//-----------------------------------------------------------------------------
void NinePinDevice::rewind ()
{
	IValue* value = deviceInterface->createParamInterfaceByID ("rewind");
	FReleaser r (value);
	if (value)
		value->setValue2 (1, true);
}

//-----------------------------------------------------------------------------
void NinePinDevice::record ()
{
	IValue* value = deviceInterface->createParamInterfaceByID ("record");
	FReleaser r (value);
	if (value)
		value->setValue2 (1, true);
}

}