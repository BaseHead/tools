//-----------------------------------------------------------------------------
// Project     : Virtual File Systems
// Version     : 1.0
//
// Filename    : virtualfilesys.cpp
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

#include "public.sdk/source/filesystem/virtualfilesys.h"

namespace Steinberg {

//------------------------------------------------------------------------
tresult PLUGIN_API VirtualFileSystem::openFile (IPath* path, uint32 openModeFlags, IBStream** result)
{
	return kNotImplemented;
}

//------------------------------------------------------------------------
tresult PLUGIN_API VirtualFileSystem::fileExists (IPath* path, bool* result)
{
	return kNotImplemented;
}

//------------------------------------------------------------------------
tresult PLUGIN_API VirtualFileSystem::getFileSize (IPath* path, int64* res)
{
	return kNotImplemented;
}

//------------------------------------------------------------------------
IVFileSystemIterator* PLUGIN_API VirtualFileSystem::createIterator (bool recurseIntoFolders, const IPath* root)
{
	return 0;
}

//------------------------------------------------------------------------
tresult PLUGIN_API VirtualFileSystem::createFile (IPath* path, uint32 openModeFlags, IBStream** result)
{
	return kNotImplemented;
}


//------------------------------------------------------------------------
tresult PLUGIN_API VirtualFileSystem::remove (IPath* path)
{
	return kNotImplemented;
}

//------------------------------------------------------------------------
tresult PLUGIN_API VirtualFileSystem::rename (IPath* path, const tchar* newName)
{
	return kNotImplemented;
}

//------------------------------------------------------------------------
tresult PLUGIN_API VirtualFileSystem::move (IPath* path, IPath* newPath, bool assignOldPath)
{
	return kNotImplemented;
}

//------------------------------------------------------------------------
tresult PLUGIN_API VirtualFileSystem::createDirectory (IPath* path)
{
	return kNotImplemented;
}

}// namespace
