//------------------------------------------------------------------------
//
// Project     : Steinberg Plug-In SDK
// Filename    : public.sdk/source/common/pattributes.h
// Created by  : Steinberg 09.2004 
// Description : Convenience wrapper for handling IAttributes
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

#pragma once

#include "pluginterfaces/base/ipersistent.h"

namespace Steinberg {

//------------------------------------------------------------------------
//  PAttributes namespace declaration
//------------------------------------------------------------------------

namespace PAttributes
{
	bool set (IAttributes* a, IAttrID attrID, int64 data);
	bool set (IAttributes* a, IAttrID attrID, char);
	bool set (IAttributes* a, IAttrID attrID, short);  
	bool set (IAttributes* a, IAttrID attrID, int);    
	bool set (IAttributes* a, IAttrID attrID, long);   
	bool set (IAttributes* a, IAttrID attrID, unsigned char);  
	bool set (IAttributes* a, IAttrID attrID, unsigned short); 
	bool set (IAttributes* a, IAttrID attrID, unsigned int);
	bool set (IAttributes* a, IAttrID attrID, unsigned long); 
	bool set (IAttributes* a, IAttrID attrID, double data);
	bool set (IAttributes* a, IAttrID attrID, float data);
	bool set (IAttributes* a, IAttrID attrID, const char8*); 
	bool set (IAttributes* a, IAttrID attrID, const char16*); 

	bool get (IAttributes* a, IAttrID attrID, int64& data);
	bool get (IAttributes* a, IAttrID attrID, char&);
	bool get (IAttributes* a, IAttrID attrID, short&);  
	bool get (IAttributes* a, IAttrID attrID, int&);    
	bool get (IAttributes* a, IAttrID attrID, long&);   
	bool get (IAttributes* a, IAttrID attrID, unsigned char&);  
	bool get (IAttributes* a, IAttrID attrID, unsigned short&); 
	bool get (IAttributes* a, IAttrID attrID, unsigned int&);  
	bool get (IAttributes* a, IAttrID attrID, unsigned long&); 
	bool get (IAttributes* a, IAttrID attrID, double& data);
	bool get (IAttributes* a, IAttrID attrID, float& data);
	const char8* getString8 (IAttributes* a, IAttrID attrID);                                     
	const char16* getString16 (IAttributes* a, IAttrID attrID);                                     

#ifndef NOBOOL
	bool get (IAttributes* a, IAttrID attrID, bool&); 
#endif

	bool queueInt (IAttributes* a, IAttrID listID, int64 data);         
	bool unqueueInt (IAttributes* a, IAttrID listID, int64& data); 
	bool unqueueInt (IAttributes* a, IAttrID listID, long& data);
	bool unqueueInt (IAttributes* a, IAttrID listID, short& data);

	bool queueFloat (IAttributes* a, IAttrID listID, double data);     
	bool unqeueFloat (IAttributes* a, IAttrID listID, double& data); 

	bool queueString8 (IAttributes* a, IAttrID listID, const char8* string);
	bool unqueueString8 (IAttributes* a, IAttrID listID, const char8** string);  

	bool queueString16 (IAttributes* a, IAttrID listID, const char16* string);
	bool unqueueString16 (IAttributes* a, IAttrID listID, const char16** string);  

	bool setObject (IAttributes* a, IAttrID, FUnknown* obj, bool archiveIsOwner = false);
	FUnknown* getObject (IAttributes* a, IAttrID attrID);
	bool queueObject (IAttributes* a, IAttrID listID, FUnknown* obj, bool archiveIsOwner = false);    
	FUnknown* unqueueObject (IAttributes* a, IAttrID listID);
};

}
