//------------------------------------------------------------------------
//
// Project     : Steinberg Plug-In SDK
// Filename    : pregistry.cpp
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
#include "pregistry.h"
#endif

#include <windows.h>
#include <stdio.h>

#ifdef UNICODE
#define tsscanf wscanf
#define tsprintf wprintf
#else
#define tsscanf sscanf
#define tsprintf sprintf
#endif

//------------------------------------------------------------------------
extern "C" typedef DWORD (WINAPI *SHDeleteKeyProc)(HKEY hKey, LPCTSTR pszSubKey);

//------------------------------------------------------------------------
//  PRegistry implementation
//------------------------------------------------------------------------

PRegistry::PRegistry (void* settings)
: settings (settings)
{}

//------------------------------------------------------------------------
PRegistry::PRegistry (Type type)
{
	switch (type)
	{
		case kMachineSetting : settings = HKEY_LOCAL_MACHINE; break;
		case kUserSetting : settings = HKEY_CURRENT_USER; break;
		case kClassesSetting : settings = HKEY_CLASSES_ROOT; break;
	}
}

//------------------------------------------------------------------------
bool PRegistry::readLong (const tchar* path, const tchar* name, long& value)
{
	tchar s[100] = {0};
	if (readString (path, name, s, 100))
	{
		int i = 0;
		if (tsscanf (s, TEXT ("%d"), &i) == 1)
		{
			value = i;
			return true;
		}
	}
	return false;
}

//------------------------------------------------------------------------
bool PRegistry::writeLong (const tchar* path, const tchar* name, long value)
{
	tchar s[100];
	tsprintf (s, TEXT ("%d"), value);
	return writeString (path, name, s);
}

//------------------------------------------------------------------------
bool PRegistry::readString (const tchar* path, const tchar* name, tchar* string, int length)
{
	HKEY hKey;
	if (RegOpenKeyEx ((HKEY)settings, path, 0, KEY_QUERY_VALUE, &hKey) == ERROR_SUCCESS)
	{
		DWORD type;
		DWORD size = length;

		long result = RegQueryValueEx (hKey, name, 0, &type, (LPBYTE)string, &size);
		RegCloseKey (hKey);
		
		if (result == ERROR_SUCCESS && type == REG_SZ)
			return true;
	}

	return false;
}

//------------------------------------------------------------------------
bool PRegistry::writeString (const tchar* path, const tchar* name, const tchar* string)
{
	HKEY hKey;
	long result;

	result = RegOpenKeyEx ((HKEY)settings, path, 0, KEY_WRITE, &hKey);
	if (result != ERROR_SUCCESS)
	{
		DWORD disposition;
		result = RegCreateKeyEx ((HKEY)settings, path, 0, NULL, REG_OPTION_NON_VOLATILE, KEY_WRITE, NULL, &hKey, &disposition);
	}

	if (result != ERROR_SUCCESS)
		return false;

	result = RegSetValueEx (hKey, name, 0, REG_SZ, (LPBYTE)string, (DWORD)(tstrlen (string) + 1) * sizeof (tchar)); // ??? sizeof (tchar)
	RegCloseKey (hKey);

	return result == ERROR_SUCCESS;
}


//------------------------------------------------------------------------
bool PRegistry::deleteKeys (const tchar* path)
{
	bool result = false;

	if (RegDeleteKey ((HKEY)settings, path) != ERROR_SUCCESS) // should work on Win95
	{
		HMODULE hLib = LoadLibrary (TEXT("Shlwapi.dll")); // otherwise try shell function
		if (hLib)
		{
			SHDeleteKeyProc proc = (SHDeleteKeyProc)GetProcAddress (hLib, "SHDeleteKey");
			if (proc)
				result = proc ((HKEY)settings, path) == ERROR_SUCCESS;

			FreeLibrary (hLib);
		}
	}

	return result;
}

//------------------------------------------------------------------------
long PRegistry::countSubKeys (const tchar* path)
{
	unsigned long count = 0;
	
	HKEY hKey;
	if (RegOpenKeyEx ((HKEY)settings, path, 0, KEY_QUERY_VALUE, &hKey) == ERROR_SUCCESS)
	{
		RegQueryInfoKey (hKey, 0, 0, 0, &count, 0, 0, 0, 0, 0, 0, 0);
		RegCloseKey (hKey);
	}

	return count;
}

//------------------------------------------------------------------------
bool PRegistry::getSubKey (const tchar* path, long index, tchar* name, int l)
{
	bool result = false;

	HKEY hKey;
	if (RegOpenKeyEx ((HKEY)settings, path, 0, KEY_ENUMERATE_SUB_KEYS, &hKey) == ERROR_SUCCESS)
	{
		unsigned long length = l;
		result = RegEnumKeyEx (hKey, index, name, &length, 0, 0, 0, 0) == ERROR_SUCCESS;
		RegCloseKey (hKey);
	}

	return result;
}
