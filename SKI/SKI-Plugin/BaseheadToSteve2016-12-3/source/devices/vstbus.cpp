//-----------------------------------------------------------------------------
// Project      : SKI - Devices
// Filename     : vstbus.cpp
// Created by   : Steinberg
// Description  : Wrapper class to Vst::IBusDescriptor
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
//----------------------------------------------------------------------------------

#include "vstbus.h"
#include "pluginterfaces/host/ihostclasses.h"

namespace Steinberg {
namespace Vst {
	
//-----------------------------------------------------------------
BusDescriptor::BusDescriptor (IHostClasses* hostClasses)
: descriptor (0)
, childBuses (0)
, childBusCount (0)
{
	descriptor = FHostCreate (IBusDescriptor, hostClasses);
}
	
//-----------------------------------------------------------------
BusDescriptor::BusDescriptor (IBusDescriptor* d)
: descriptor (d)
, childBuses (0)
, childBusCount (0)
{
	if (descriptor)
		descriptor->addRef ();
}

//-----------------------------------------------------------------
BusDescriptor::~BusDescriptor ()
{
	if (descriptor)
		descriptor->release ();

	resetChildBuses ();
}

//-----------------------------------------------------------------
void BusDescriptor::resetChildBuses ()
{
	if (childBuses)
	{
		for (int32 i = 0; i < childBusCount; i++)
			delete childBuses[i];
		delete childBuses;
		childBuses = 0;
		childBusCount = 0;
	}
}


//-----------------------------------------------------------------
void BusDescriptor::setupChildBuses ()
{
	FUnknownPtr<IBusDescriptor2> descr2 (descriptor);
	if (descr2)
	{
		int32 busCount = descr2->countChildBuses ();
		if (busCount != childBusCount)
		{
			resetChildBuses ();

			if (busCount > 0)
			{
				childBuses = new BusDescriptor* [busCount];
				for (int32 i = 0; i < busCount; i++)
				{
					childBuses [i] = 0;
				
					IBusDescriptor* child = descr2->getChildDescriptor (i);
					if (child)
						childBuses [i] = new BusDescriptor (child); 
				}
				childBusCount = busCount;
			}
		}
	}
}

//-----------------------------------------------------------------
SpeakerArrangement BusDescriptor::getArrangement () const
{
	SpeakerArrangement result = 0;
	if (descriptor)
	{
		for (int32 i = 0; i < descriptor->countPins (); i++)
		{
			result |= descriptor->getPinSpeaker (i);
		}	
	}
	return result;
}

//-----------------------------------------------------------------
bool BusDescriptor::createPins (SpeakerArrangement speakerArrangement)
{
	if (descriptor)
		return descriptor->createPins (speakerArrangement) == kResultTrue;
	return false;
}

//-----------------------------------------------------------------
int32 BusDescriptor::countPins ()
{
	if (descriptor)
		return descriptor->countPins ();
	return 0;
}

//-----------------------------------------------------------------
SpeakerArrangement BusDescriptor::getPinSpeaker (int32 pinIndex)
{
	if (descriptor)
		return descriptor->getPinSpeaker (pinIndex);
	return 0;
}

//-----------------------------------------------------------------
bool BusDescriptor::setPinConnection (int32 pinIndex, IPort* port)
{
	if (descriptor)
		return descriptor->setPinConnection (pinIndex, port) == kResultTrue;
	return false;
}

//-----------------------------------------------------------------
IPort* BusDescriptor::getPinConnection (int32 pinIndex)
{
	return descriptor ? descriptor->getPinConnection (pinIndex) : 0;
}

//-----------------------------------------------------------------
bool BusDescriptor::reset ()
{
	resetChildBuses ();
	if (descriptor)
		return descriptor->removeAllPins () == kResultTrue;
	return false;
}

//-----------------------------------------------------------------
int32 BusDescriptor::countChildBuses ()
{
	setupChildBuses ();
	
	return childBusCount;
}

//-----------------------------------------------------------------
BusDescriptor* BusDescriptor::getChildBus (int32 index)
{
	setupChildBuses ();
	if (index >= 0 && index < childBusCount && childBuses != 0)
		return childBuses [index];
	return 0;
}

//-----------------------------------------------------------------
BusDescriptor* BusDescriptor::getBusByArrangement (SpeakerArrangement arr)
{
	if (arr == getArrangement ())
		return this;
	setupChildBuses ();
	for (int32 i = 0; i < childBusCount; i++)
	{
		BusDescriptor* bus = childBuses [i] ? childBuses [i]->getBusByArrangement (arr) : 0;
		if (bus)
			return bus;
	}
	return 0;
}


}} // namespace