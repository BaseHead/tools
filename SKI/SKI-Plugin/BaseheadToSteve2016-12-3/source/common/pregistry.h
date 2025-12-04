//------------------------------------------------------------------------
//
// Project     : Steinberg Plug-In SDK
// Filename    : pregistry.h
// Created by  : Steinberg 31.05.2001
// Description : registry access
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

#ifndef __pregistry__
#define __pregistry__

#include "pluginterfaces/base/fstrdefs.h"
using namespace Steinberg;

//------------------------------------------------------------------------
//  PRegistry class declaration
//------------------------------------------------------------------------

class PRegistry
{
public:
//------------------------------------------------------------------------
	enum Type
	{
		kMachineSetting,
		kUserSetting,
		kClassesSetting
	};

	PRegistry (void* settings); // HKEY
	PRegistry (Type type);

	bool readString (const tchar* path, const tchar* name, tchar* string, int length = 1024);
	bool writeString (const tchar* path, const tchar* name, const tchar* string);

	bool readLong (const tchar* path, const tchar* name, long& value);
	bool writeLong (const tchar* path, const tchar* name, long value);
	
	bool deleteKeys (const tchar* path);

	long countSubKeys (const tchar* path);
	bool getSubKey (const tchar* path, long index, tchar* name, int length = 256);
//------------------------------------------------------------------------
protected:
	void* settings;
};

#endif