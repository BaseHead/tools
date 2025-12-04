//-----------------------------------------------------------------------------
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

#include "fileutils.h"

#if MAC
#include <CoreFoundation/CoreFoundation.h>
#endif

#include "pluginterfaces/host/frame/itranslator.h"
#include "pluginterfaces/host/ihostclasses.h"

//------------------------------------------------------------------------
bool convertPathStringToPlatformString (char* inOutString, int32 stringSize, IHostClasses* hostClasses)
{
	#if MAC
	FInstancePtr<ITranslator> translator (hostClasses);
	if (translator)
	{
		char lang[3] = {0};
		if (translator->getLanguage (lang) == kResultTrue)
		{
			CFStringRef cfStr = 0;
			if (!strcmp (lang, "jp"))
				cfStr = CFStringCreateWithCString (0, inOutString, kCFStringEncodingShiftJIS_X0213_00);
			else
				cfStr = CFStringCreateWithCString (0, inOutString, kCFStringEncodingMacRoman);
			if (cfStr)
			{
				CFStringGetCString (cfStr, inOutString, stringSize, kCFStringEncodingUTF8);
				CFRelease (cfStr);
				return true;
			}
		}
	}
	return false;
	#else
	return true;

	#endif
}

//------------------------------------------------------------------------
bool convertPlatformStringToPathString (char* inOutString, int32 stringSize, IHostClasses* hostClasses)
{
	#if MAC
	FUnknownPtr<ITranslator> translator = FHostCreate (ITranslator, hostClasses);
	if (translator)
	{
		char lang[3] = {0};
		if (translator->getLanguage (lang) == kResultTrue)
		{
			CFStringRef cfStr = CFStringCreateWithCString (0, inOutString, kCFStringEncodingUTF8);
			if (cfStr)
			{
				if (!strcmp (lang, "jp"))
					CFStringGetCString (cfStr, inOutString, stringSize, kCFStringEncodingShiftJIS_X0213_00);
				else
					CFStringGetCString (cfStr, inOutString, stringSize, kCFStringEncodingMacRoman);
				CFRelease (cfStr);
				return true;
			}
		}
	}
	return false;
	#else
	return true;

	#endif
}

