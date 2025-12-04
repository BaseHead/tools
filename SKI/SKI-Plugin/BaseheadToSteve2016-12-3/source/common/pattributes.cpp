//------------------------------------------------------------------------
//
// Project     : Steinberg Plug-In SDK
// Filename    : pattributes.cpp
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


#include "pattributes.h"
#include "pluginterfaces/base/fvariant.h"

namespace Steinberg {

//------------------------------------------------------------------------
//  PAttributes namespace definition
//------------------------------------------------------------------------

//------------------------------------------------------------------------
bool PAttributes::set (IAttributes* a, IAttrID attrID, int64 data)
{
	FVariant variant;
	variant.setInt (data);
	if (a->set (attrID, variant) == kResultTrue)
		return true;
	return false;
}	

//------------------------------------------------------------------------
bool PAttributes::set (IAttributes* a, IAttrID attrID, char data)
{
	return set (a, attrID, (int64) data);
}

//------------------------------------------------------------------------
bool PAttributes::set (IAttributes* a, IAttrID attrID, short data)
{
	return set (a, attrID, (int64) data);
}

//------------------------------------------------------------------------
bool PAttributes::set (IAttributes* a, IAttrID attrID, int data)  
{
	return set (a, attrID, (int64) data);
}

//------------------------------------------------------------------------
bool PAttributes::set (IAttributes* a, IAttrID attrID, long data) 
{
	return set (a, attrID, (int64) data);
}

//------------------------------------------------------------------------
bool PAttributes::set (IAttributes* a, IAttrID attrID, unsigned char data)
{
	return set (a, attrID, (int64) data);
}

//------------------------------------------------------------------------
bool PAttributes::set (IAttributes* a, IAttrID attrID, unsigned short data)
{
	return set (a, attrID, (int64) data);
}

//------------------------------------------------------------------------
bool PAttributes::set (IAttributes* a, IAttrID attrID, unsigned int data)
{
	return set (a, attrID, (int64) data);
}

//------------------------------------------------------------------------
bool PAttributes::set (IAttributes* a, IAttrID attrID, unsigned long data)
{
	return set (a, attrID, (int64) data);
}

//------------------------------------------------------------------------
bool PAttributes::set (IAttributes* a, IAttrID attrID, double data)
{
	FVariant variant;
	variant.setFloat (data);
	if (a->set (attrID, variant) == kResultTrue)
		return true;
	return false;
}

//------------------------------------------------------------------------
bool PAttributes::set (IAttributes* a, IAttrID attrID, float data)
{
	return set (a, attrID, (double) data);
}

//------------------------------------------------------------------------
bool PAttributes::set (IAttributes* a, IAttrID attrID, const char8* data)
{
	FVariant variant;
	variant.setString8 (data);
	if (a->set (attrID, variant) == kResultTrue)
		return true;
	return false;
}

//------------------------------------------------------------------------
bool PAttributes::set (IAttributes* a, IAttrID attrID, const char16* data)
{
	FVariant variant;
	variant.setString16 (data);
	if (a->set (attrID, variant) == kResultTrue)
		return true;
	return false;
}

//------------------------------------------------------------------------
bool PAttributes::get (IAttributes* a, IAttrID attrID, int64& data)
{
	FVariant variant;
	if (a->get (attrID, variant) == kResultTrue)
	{
		data = variant.getInt ();
		return true;
	}
	return false;
}

//------------------------------------------------------------------------
bool PAttributes::get (IAttributes* a, IAttrID attrID, char& data)
{
	FVariant variant;
	if (a->get (attrID, variant) == kResultTrue)
	{
		data = (char)variant.getInt ();
		return true;
	}
	return false;
}

//------------------------------------------------------------------------
bool PAttributes::get (IAttributes* a, IAttrID attrID, short& data)
{
	FVariant variant;
	if (a->get (attrID, variant) == kResultTrue)
	{
		data = (short)variant.getInt ();
		return true;
	}
	return false;
}

//------------------------------------------------------------------------
bool PAttributes::get (IAttributes* a, IAttrID attrID, int& data)
{
	FVariant variant;
	if (a->get (attrID, variant) == kResultTrue)
	{
		data = (int)variant.getInt ();
		return true;
	}
	return false;
}

//------------------------------------------------------------------------
bool PAttributes::get (IAttributes* a, IAttrID attrID, long& data)
{
	FVariant variant;
	if (a->get (attrID, variant) == kResultTrue)
	{
		data = (long) variant.getInt ();
		return true;
	}
	return false;
}

//------------------------------------------------------------------------
bool PAttributes::get (IAttributes* a, IAttrID attrID, unsigned char& data)
{
	FVariant variant;
	if (a->get (attrID, variant) == kResultTrue)
	{
		data = (unsigned char)variant.getInt ();
		return true;
	}
	return false;
}

//------------------------------------------------------------------------
bool PAttributes::get (IAttributes* a, IAttrID attrID, unsigned short& data)
{
	FVariant variant;
	if (a->get (attrID, variant) == kResultTrue)
	{
		data = (unsigned short)variant.getInt ();
		return true;
	}
	return false;
}

//------------------------------------------------------------------------
bool PAttributes::get (IAttributes* a, IAttrID attrID, unsigned int& data)
{
	FVariant variant;
	if (a->get (attrID, variant) == kResultTrue)
	{
		data = (unsigned int)variant.getInt ();
		return true;
	}
	return false;
}

//------------------------------------------------------------------------
bool PAttributes::get (IAttributes* a, IAttrID attrID, unsigned long& data)
{
	FVariant variant;
	if (a->get (attrID, variant) == kResultTrue)
	{
		data = (unsigned long)variant.getInt ();
		return true;
	}
	return false;
}

#ifndef NOBOOL
bool PAttributes::get (IAttributes* a, IAttrID attrID, bool& data)
{
	FVariant variant;
	if (a->get (attrID, variant) == kResultTrue)
	{
		data = variant.getInt () != 0;
		return true;
	}
	return false;
}
#endif

//------------------------------------------------------------------------
bool PAttributes::get (IAttributes* a, IAttrID attrID, double& data)
{
	FVariant variant;
	if (a->get (attrID, variant) == kResultTrue)
	{
		data = variant.getFloat ();
		return true;
	}
	return false;
}

//------------------------------------------------------------------------
bool PAttributes::get (IAttributes* a, IAttrID attrID, float& data)
{
	FVariant variant;
	if (a->get (attrID, variant) == kResultTrue)
	{
		data = (float)variant.getFloat ();
		return true;
	}
	return false;
}

//------------------------------------------------------------------------
const char8* PAttributes::getString8 (IAttributes* a, IAttrID attrID)
{
	FVariant variant;
	if (a->get (attrID, variant) == kResultTrue)
		return variant.getString8 ();
	return 0;
}

//------------------------------------------------------------------------
const char16* PAttributes::getString16 (IAttributes* a, IAttrID attrID)
{
	FVariant variant;
	if (a->get (attrID, variant) == kResultTrue)
		return variant.getString16 ();
	return 0;
}

//------------------------------------------------------------------------
bool PAttributes::queueInt (IAttributes* a, IAttrID listID, int64 data)
{
	FVariant variant;
	variant.setInt (data);
	if (a->queue (listID, variant) == kResultTrue)
		return true;
	return false;
}

//------------------------------------------------------------------------
bool PAttributes::unqueueInt (IAttributes* a, IAttrID listID, int64& data)
{
	FVariant variant;
	if (a->unqueue (listID, variant) == kResultTrue)
	{
		data = variant.getInt ();
		return true;
	}
	return false;
}

//------------------------------------------------------------------------
bool PAttributes::unqueueInt (IAttributes* a, IAttrID listID, long& data)
{
	FVariant variant;
	if (a->unqueue (listID, variant) == kResultTrue)
	{
		data = (long)variant.getInt ();
		return true;
	}
	return false;
}

//------------------------------------------------------------------------
bool PAttributes::unqueueInt (IAttributes* a, IAttrID listID, short& data)
{
	FVariant variant;
	if (a->unqueue (listID, variant) == kResultTrue)
	{
		data = (short)variant.getInt ();
		return true;
	}
	return false;
}

//------------------------------------------------------------------------
bool PAttributes::queueFloat (IAttributes* a, IAttrID listID, double data)
{
	FVariant variant;
	variant.setFloat (data);
	if (a->queue (listID, variant) == kResultTrue)
		return true;
	return false;
}

//------------------------------------------------------------------------
bool PAttributes::unqeueFloat (IAttributes* a, IAttrID listID, double& data)
{
	FVariant variant;
	if (a->unqueue (listID, variant) == kResultTrue)
	{
		data = variant.getFloat ();
		return true;
	}
	return false;
}

//------------------------------------------------------------------------
bool PAttributes::queueString8 (IAttributes* a, IAttrID listID, const char8* string)
{
	FVariant variant;
	variant.setString8 (string);
	if (a->queue (listID, variant) == kResultTrue)
		return true;
	return false;
}

//------------------------------------------------------------------------
bool PAttributes::unqueueString8 (IAttributes* a, IAttrID listID, const char8** string)
{
	FVariant variant;
	if (a->unqueue (listID, variant) == kResultTrue)
	{
		*string = variant.getString8 ();
		return true;
	}
	return false;
}

//------------------------------------------------------------------------
bool PAttributes::queueString16 (IAttributes* a, IAttrID listID, const char16* string)
{
	FVariant variant;
	variant.setString16 (string);
	if (a->queue (listID, variant) == kResultTrue)
		return true;
	return false;
}

//------------------------------------------------------------------------
bool PAttributes::unqueueString16 (IAttributes* a, IAttrID listID, const char16** string)
{
	FVariant variant;
	if (a->unqueue (listID, variant) == kResultTrue)
	{
		*string = variant.getString16 ();
		return true;
	}
	return false;
}


//------------------------------------------------------------------------
bool PAttributes::setObject (IAttributes* a, IAttrID attrID, FUnknown* obj, bool archiveIsOwner)
{
	FVariant variant;
	variant.setObject (obj);
	if (archiveIsOwner)
		variant.type |= FVariant::kOwner;
	if (a->set (attrID, variant) == kResultTrue)
		return true;
	return false;
}

//------------------------------------------------------------------------
FUnknown* PAttributes::getObject (IAttributes* a, IAttrID attrID)
{
	FVariant variant;
	if (a->get (attrID, variant) == kResultTrue)
		return variant.getObject ();
	return 0;
}

//------------------------------------------------------------------------
bool PAttributes::queueObject (IAttributes* a, IAttrID listID, FUnknown* obj, bool archiveIsOwner)
{
	FVariant variant;
	variant.setObject (obj);
	if (archiveIsOwner)
		variant.type |= FVariant::kOwner;
	if (a->queue (listID, variant) == kResultTrue)
		return true;
	return false;
}

//------------------------------------------------------------------------
FUnknown* PAttributes::unqueueObject (IAttributes* a, IAttrID listID)
{
	FVariant variant;
	if (a->unqueue (listID, variant) == kResultTrue)
		return variant.getObject ();
	return 0;
}

}
