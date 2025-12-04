//------------------------------------------------------------------------
//
// Project     : Steinberg Plug-In SDK
// Filename    : public.sdk/source/common/pvaluecontainer.h
// Created by  : Steinberg 12.2003 (based on pplugparams.h)
// Description : IValue List
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
//------------------------------------------------------------------------------

#pragma once

#include "pluginterfaces/base/ftypes.h"

namespace Steinberg {

class IHostClasses;
class IPlugController;
class IValue;
class PValueList;
class IDefaultPool;

//------------------------------------------------------------------------
//  PValueContainer class declaration
//------------------------------------------------------------------------

class PValueContainer
{
public:
//------------------------------------------------------------------------

	PValueContainer (IHostClasses* host, IPlugController* controller = nullptr);
	virtual ~PValueContainer ();
	void setController (IPlugController* c) { controller = c; }
	void setHostClasses (IHostClasses* host);

	IValue* addOnOffValue (int32 tag, FIDString name, bool state = false, bool automated = false);
	IValue* addIntValue (int32 tag, FIDString name, int32 min, int32 max, int32 defvalue,
	                     bool automated = false, bool wrapAround = false);
	IValue* addFloatValue (int32 tag, FIDString name, float min, float max, float defvalue,
	                       int32 precision = -1, bool automated = false, bool wrapAround = false);
	IValue* addStringValue (int32 tag, FIDString name, const tchar* text, bool automated = false);
	IValue* addStringListValue (int32 tag, FIDString name, const tchar** items,
	                            const tchar* selected = nullptr, bool automated = false);

	void initFloatValue (IValue* value, float min, float max, float defvalue, int32 precision = -1,
	                     bool automated = false, bool wrapAround = false);

	void addValue (IValue* p, int32 tag, const char* name);
	void addExternValue (IValue* p, const char* name);

	bool loadValues (FIDString defaultsID, bool updateTarget = true,
	                 IDefaultPool* defaults = nullptr);
	bool storeValues (FIDString defaultsID, IDefaultPool* defaults = nullptr);

	int32 countValues ();
	bool getValueName (int32 index, char8 str[128]); // id of value

	IValue* getValue (FIDString name);
	IValue* getValueByIndex (int32 index);
	IValue* getValueByTag (int32 tag);

	void setValueActive (FIDString name, bool state);
	void setValueActive (int32 tag, bool state);

	void removeAll ();

//------------------------------------------------------------------------
protected:
	IHostClasses* host;
	IPlugController* controller;

private:
	PValueList* values;
};
}
