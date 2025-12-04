//-----------------------------------------------------------------------------
// Project     : Virtual File Systems
// Version     : 1.0
//
// Filename    : virtualfilesys.h
// Created by  : Steinberg 2009
// Description : Base class for virtual file system implementations
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

#ifndef __virtualfilesys__
#define __virtualfilesys__

#include "pluginterfaces/filesystem/ivirtualfilesystem.h"
#include "pluginterfaces/base/ibstream.h"
#include "base/source/fobject.h"

namespace Steinberg {

//------------------------------------------------------------------------
// VirtualFileSystem
//------------------------------------------------------------------------
class VirtualFileSystem : public FObject, public IVirtualFileSystem
{
public:
//------------------------------------------------------------------------
	virtual tresult PLUGIN_API openFile (IPath* path, uint32 openModeFlags, IBStream** result);
	virtual tresult PLUGIN_API fileExists (IPath* path, bool* result = 0);
	virtual tresult PLUGIN_API getFileSize (IPath* path, int64* res);
	virtual IVFileSystemIterator* PLUGIN_API createIterator (bool recurseIntoFolders, const IPath* root); 

	virtual tresult PLUGIN_API createFile (IPath* path, uint32 openModeFlags, IBStream** result);
	virtual tresult PLUGIN_API remove (IPath* path);
	virtual tresult PLUGIN_API rename (IPath* path, const tchar* newName);
	virtual tresult PLUGIN_API move (IPath* path, IPath* newPath, bool assignOldPath);
	virtual tresult PLUGIN_API createDirectory (IPath* path);

	OBJ_METHODS (VirtualFileSystem, FObject)
	FUNKNOWN_METHODS (IVirtualFileSystem, FObject)
//------------------------------------------------------------------------
};

//------------------------------------------------------------------------
// File system based on a stream
//------------------------------------------------------------------------
class ArchiveFileSystem : public VirtualFileSystem
{
public:
//------------------------------------------------------------------------
	ArchiveFileSystem (IBStream* stream) : archiveStream (stream) {}

	IBStream* getArchiveStream () const {return archiveStream;}

	OBJ_METHODS (ArchiveFileSystem, VirtualFileSystem)
//------------------------------------------------------------------------
private:
	IPtr<IBStream> archiveStream;
};


} // namespace

#endif
