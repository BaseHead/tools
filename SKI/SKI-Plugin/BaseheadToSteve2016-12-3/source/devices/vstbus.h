//-----------------------------------------------------------------------------
// Project      : SKI - Devices
// Filename     : vstbus.h
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

#include "pluginterfaces/host/devices/ivstbus.h"

namespace Steinberg {
class IHostClasses;

namespace Vst {


//-----------------------------------------------------------------
// Wrapper for Vst::IBusDescriptor 
//-----------------------------------------------------------------
class BusDescriptor
{
public:
//-----------------------------------------------------------------
	BusDescriptor (IHostClasses* hostClasses);
	BusDescriptor (IBusDescriptor* descriptor);
	~BusDescriptor ();
	operator IBusDescriptor* () {return descriptor;}
	IBusDescriptor* getInterface () {return descriptor;}

	SpeakerArrangement getArrangement () const;

	bool createPins (SpeakerArrangement speakerArrangement);
	int32 countPins ();

	SpeakerArrangement getPinSpeaker (int32 pinIndex);

	bool setPinConnection (int32 pinIndex, IPort* port);
	IPort* getPinConnection (int32 pinIndex); 

	bool reset ();

	int32 countChildBuses ();
	BusDescriptor* getChildBus (int32 index);
	BusDescriptor* getBusByArrangement (SpeakerArrangement arr);

//-----------------------------------------------------------------
protected:
	void setupChildBuses ();
	void resetChildBuses ();

	IBusDescriptor* descriptor;
	BusDescriptor** childBuses;
	int32 childBusCount;
};

}}