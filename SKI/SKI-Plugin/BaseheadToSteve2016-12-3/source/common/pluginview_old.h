//-----------------------------------------------------------------------------
// Project     : SDK Core
//
// Category    : Common Base Classes
// Filename    : pluginview.h
// Created by  : Steinberg, 01/2004
// Description : Plug-In View Implementation
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

#ifndef __pluginview_old__
#define __pluginview_old__

#ifndef __iplugview__
#include "pluginterfaces/gui/iplugview_old.h"
#endif

//------------------------------------------------------------------------
//  CPluginView : plugin view base class
//------------------------------------------------------------------------
namespace Steinberg {

class CPluginView: public IPlugView2Obsolete
{
public:
//------------------------------------------------------------------------
	CPluginView (const ViewRect* rect = 0);
	virtual ~CPluginView ();

	/** Returns its current frame rect. */
	const ViewRect& getRect () const	{ return rect; }
	/** Set a new frame rect. */
	void setRect (const ViewRect& r)	{ rect = r; }

	/** Check if this view is attached to its parent view. */
	bool isAttached () const			{ return systemWindow != 0; }

//------------------------------------------------------------------------
	// Interface methods:
	DECLARE_FUNKNOWN_METHODS

	//---from IPlugViewObsolete-------
	tresult PLUGIN_API attached (void* parent);
	tresult PLUGIN_API removed ();

	tresult PLUGIN_API onIdle () { return kResultFalse; }
	tresult PLUGIN_API onWheel (float distance) { return kResultFalse; }
	tresult PLUGIN_API onKey (char asciiKey, int32 keyMsg, int32 modifiers) { return kResultFalse; }
	tresult PLUGIN_API onSize (ViewRect* newSize);

	//--from IPlugView2Obsolete------
	tresult PLUGIN_API onFocus (TBool state){ return kResultFalse; }
	tresult PLUGIN_API getSize (ViewRect* size);
	tresult PLUGIN_API setFrame (IPlugFrameObsolete* frame) {return kResultFalse;}
	tresult PLUGIN_API onKeyUp (char asciiKey, int32 keyCode, int32 modifiers) { return kResultFalse; }
//------------------------------------------------------------------------
protected:
	ViewRect rect;
	void* systemWindow;
};
}
#endif	// __pluginview__
