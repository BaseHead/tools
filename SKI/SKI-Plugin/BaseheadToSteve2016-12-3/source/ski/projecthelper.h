//---------------------------------------------------------------------------------
// Steinberg Module Architecture SDK
// Version 1.0    Date : 2011
//
// Category     : SKI
// Filename     : projecthelper.h
// Created by   : Steinberg
// Description  : helper classes for accessing project data
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

#ifndef __projecthelper__
#define __projecthelper__

#include "pluginterfaces/host/project/iprojectobjects.h"
#include "pluginterfaces/host/project/iprojectinfo.h"

#include "base/source/tstack.h"

namespace Steinberg {
namespace ProjectHelper {

//----------------------------------------------------------------------------------
// TrackIterator
//----------------------------------------------------------------------------------
class TrackIterator
{
public:
//----------------------------------------------------------------------------------
	TrackIterator (IProject* project)
	{
		rootObject = FUnknownPtr<IProjectObject> (project);
	}
	TrackIterator (IProjectObject* folderTrackOrProject)
	: rootObject (folderTrackOrProject)
	{		
	}

	ITrack* getNextTrack ()
	{
		if (iteratorStack.isEmpty () && rootObject)
		{
			OPtr<IProjectIterator> iter = rootObject->createIterator ();
			if (iter)
				iteratorStack.push (iter);
		}
	
		while (true)
		{
			IProjectIterator* currentIterator = iteratorStack.peek ();
			if (currentIterator)
			{
				while (true)
				{				
					IProjectObject* obj = currentIterator->getNextObject ();
					if (obj)
					{
						FUnknownPtr<ITrack> track (obj);
						if (track == 0)
						{
							iteratorStack.pop ();
							break;						
						}
						if (obj->isObjectType (kFolderObject))
						{
							OPtr<IProjectIterator> iter = obj->createIterator ();
							if (iter)
								iteratorStack.push (iter);							
						}
						return track;
					}
					else
					{
						iteratorStack.pop ();
						break;						
					}						
				}
			}
			else
			{
				break;
			}
		}
		return 0;
	}

//----------------------------------------------------------------------------------
private:
	IPtr<IProjectObject> rootObject;
	TStack< IPtr<IProjectIterator> > iteratorStack;
};



}}

#endif