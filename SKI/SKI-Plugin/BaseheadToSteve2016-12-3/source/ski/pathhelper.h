//---------------------------------------------------------------------------------
// Steinberg Module Architecture SDK
// Version 1.0    Date : 2012
//
// Category     : SKI
// Filename     : pathhelper.h
// Created by   : Steinberg
// Description  : helper classes for accessing host objects
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

#ifndef __pathhelper__
#define __pathhelper__

#include "pluginterfaces/host/frame/ipath.h"
#include "pluginterfaces/host/ihostclasses.h"
#include "pluginterfaces/host/ihostapplication.h"
#include "base/source/fstring.h"

namespace Steinberg {
namespace PathHelper {


#if WINDOWS
	#define DELIMITER '\\'
#else
	#define DELIMITER '/'
#endif


//-----------------------------------------------------------
static bool checkType (const IPath& path, IPath::IPathType t)
{
	int32 type = 0;
	const_cast<IPath&> (path).getType (&type);
	return type == t;
}

//-----------------------------------------------------------
static bool isFile (const IPath& path) { return checkType (path, IPath::kIPFile); }
static bool isDirectory (const IPath& path) { return checkType (path, IPath::kIPDirectory); }
static bool isLink (const IPath& path) { return checkType (path, IPath::kIPLink); }
static bool isBundle (const IPath& path) { return checkType (path, IPath::kIPBundle); }

//-----------------------------------------------------------
// file name string from IPath
//-----------------------------------------------------------
struct FileNameString : public String
{
	FileNameString (const IPath* path)
	{
		if (path)
		{
			tchar buffer [kIPPathNameMax] = {0};
			if (const_cast<IPath*>(path)->getFileName (buffer) == kResultOk)
			{
				this->assign (buffer);
			}
		}
	}
};

//-----------------------------------------------------------
// path (w.o. file name) string from IPath
//-----------------------------------------------------------
struct PathNameString : public String
{
	PathNameString (const IPath* path)
	{
		if (path)
		{
			tchar buffer [kIPPathNameMax] = {0};
			if (const_cast<IPath*>(path)->getPathName (buffer) == kResultOk)
			{
				this->assign (buffer);
			}
		}
	}
};

//-----------------------------------------------------------
// full path string from IPath
//-----------------------------------------------------------
struct FullPathString : public String
{
	FullPathString (const IPath* path)
	{
		if (path)
		{
			tchar buffer [kIPPathNameMax] = {0};
			if (const_cast<IPath*>(path)->getFullPath (buffer) == kResultOk)
			{
				this->assign (buffer);
			}
		}
	}
};

//-----------------------------------------------------------
// standard locations
//-----------------------------------------------------------
static IPath* createPathToUserFolder (IHostClasses* hostClasses)
{
	return FHostCreate (IUserFolder, hostClasses);
}

//-----------------------------------------------------------
static IPath* createPathToApplicationFolder (IHostClasses* hostClasses)
{
	FUnknownPtr<IHostApplicationW> hostApp (hostClasses);
	if (hostApp == 0)
		return 0;

	IPath* path = FHostCreate (IPath, hostClasses);
	if (path == 0)
		return 0;

	char16 pathBuffer [kIPPathNameMax] = {0};
	hostApp->getApplicationPathW (pathBuffer);

	path->setFullPath (pathBuffer, IPath::kIPDirectory);
	return path;
}


//------------------------------------------------------------------------
static bool isInDir (const IPath& path, const IPath& dir, bool caseSensitive)
{
	FullPathString pa (&path);
	FullPathString pb (&dir);

	int32 aTotalLen = pa.length ();
	int32 bTotalLen = pb.length ();

	if (bTotalLen >= aTotalLen || bTotalLen == 0)
		return false;

	bool paWide = pa.isWideString ();
	bool pbWide = pb.isWideString ();

	int32 index = 0;
	char16 a;
	char16 b;

	while (index < bTotalLen)
	{
		a = paWide ? pa.getChar16 (index) : (char16)pa.getChar8 (index);
		b = pbWide ? pb.getChar16 (index) : (char16)pb.getChar8 (index);

		if (a == b)
		{
			index++;
			continue;
		}

		if (caseSensitive == false)
		{
			if ((a << 8) == 0)
				a = ConstString::toLower ((char8)a);
			else
				a = ConstString::toLower (a);

			if ((b << 8) == 0)
				b = ConstString::toLower ((char8)b);
			else
				b = ConstString::toLower (b);
		}

		if (a != b)
			return false;

		index++;
	}

	// now that a is longer than b 
	// a has to start a new folder to be inside b

	// if a is a delimiter, then return true
	if (a == DELIMITER)
		return true;

	// if next a[index] is a delimiter, then also return true;
	a = paWide ? pa.getChar16 (index) : (char16)pa.getChar8 (index);
	if (a != DELIMITER)
		return false;

	return true;
}





}}

#endif