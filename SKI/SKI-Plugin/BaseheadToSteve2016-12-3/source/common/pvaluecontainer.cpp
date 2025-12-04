//------------------------------------------------------------------------
//
// Project     : Steinberg Plug-In SDK
// Filename    : pvaluecontainer.cpp
// Created by  : Steinberg 21.11.2001
// Description : parameter list
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

#include "pvaluecontainer.h"
#include "pluginterfaces/host/frame/ihostvalue.h"
#include "pluginterfaces/host/frame/idefaultpool.h"
#include "pluginterfaces/host/ihostclasses.h"

#include "base/source/fstring.h"
#include "base/source/tlist.h"

namespace Steinberg {

//------------------------------------------------------------------------
//  PValueList class
//------------------------------------------------------------------------

struct PValueEntry
{
	IValue* value;
	String name;

	PValueEntry (IValue* v = nullptr, FIDString name = nullptr)
	: value (v), name (name)
	{}

	~PValueEntry ()
	{
		if (value)
			value->release ();
	}
};

class PValueList: public TLinkedList<PValueEntry*>
{ 
public:
	PValueList () {error () = 0;}
	~PValueList () override { removeAndDeleteAll (); }
	
	void removeAndDeleteAll ()
	{
		FOREACH_T (PValueEntry*, entry, (*this))
			delete entry;
		ENDFOR
		removeAll();
	}
};

//------------------------------------------------------------------------
//  PValueContainer implementation
//------------------------------------------------------------------------
PValueContainer::PValueContainer (IHostClasses* hostClasses, IPlugController* controller)
: host (nullptr)
, controller (controller)
, values (new PValueList)
{
	setHostClasses (hostClasses);
}

//------------------------------------------------------------------------
PValueContainer::~PValueContainer ()
{
	delete values;
	setHostClasses (nullptr);
}

//------------------------------------------------------------------------
void PValueContainer::setHostClasses (IHostClasses* hc)
{
	if (host)
		host->release ();
	host = hc;
	if (host)
		host->addRef ();
}

//------------------------------------------------------------------------
bool PValueContainer::loadValues (FIDString defaultsID, bool updateTarget, IDefaultPool* defaults)
{
	if (!defaults)
	{
		defaults = FHostCreate (IDefaultPool, host);
	}
	else
		defaults->addRef ();

	if(!defaults)
		return false;
	
	FUnknownPtr<IDefaultPool3> def3 (defaults);

	FOREACH_T (PValueEntry*, entry, (*values))
		switch (entry->value->getType ())
		{
			case IValue::kOnOff :
			case IValue::kInt :
			{
				int32 value;
				if (defaults->getLong (defaultsID, entry->name, &value))
					entry->value->setValue2 (value, updateTarget);
				break;
			}

			case IValue::kFloat :
			{
				double value;
				if (defaults->getDouble (defaultsID, entry->name, &value))
					entry->value->setFloatValue ((float)value, updateTarget);
				break;
			}
			case IValue::kString :
			{
				if (def3)
				{
					const tchar* s = def3->getTString (defaultsID, entry->name);
					if (s)
						entry->value->fromString2 (s, updateTarget);
				}
				break;
			}
		}
	ENDFOR

	defaults->release ();

	return true;
}

//------------------------------------------------------------------------
bool PValueContainer::storeValues (FIDString defaultsID, IDefaultPool* defaults)
{
	if (!defaults)
	{
		defaults = FHostCreate (IDefaultPool, host);
	}
	else
		defaults->addRef ();

	if (!defaults)
		return false;
	
	FUnknownPtr<IDefaultPool3> def3 (defaults);

	FOREACH_T (PValueEntry*, entry, (*values))
		switch (entry->value->getType ())
		{
			case IValue::kOnOff :
			case IValue::kInt :
			{
				defaults->setLong (defaultsID, entry->name, entry->value->getValue ());
				break;
			} 
			case IValue::kFloat :
			{
				defaults->setDouble (defaultsID, entry->name, entry->value->getFloatValue ());
				break;
			} 

			case IValue::kString :
			{
				if (def3)
				{
					int32 bufferSize = 0;
					entry->value->toString2 (nullptr, &bufferSize);
					if (bufferSize > 0)
					{
						auto* buffer = new tchar [bufferSize];
						entry->value->toString2 (buffer, &bufferSize);
						def3->setTString (defaultsID, entry->name, buffer);
						delete[] buffer;
					}
					else
						def3->setTString (defaultsID, entry->name, STR (""));
				}
				break;
			}
		}
	ENDFOR

	defaults->release ();

	return true;
}

//------------------------------------------------------------------------
void PValueContainer::addValue (IValue* v, int32 tag, FIDString name)
{
	if (v && name)
	{
		if (controller)
			v->connect (controller, tag);
		values->add (new PValueEntry (v, name));
	}
}

//------------------------------------------------------------------------
void PValueContainer::addExternValue (IValue* v, FIDString name)
{
	if (v && name)
		values->add (new PValueEntry (v, name));
}

//------------------------------------------------------------------------
IValue* PValueContainer::addOnOffValue (int32 tag, FIDString name, bool state, bool automated)
{
	IValue* value = FHostCreate (IHostOnOffValue, host);
	if (value)
	{
		value->setValue2 (state ? 1 : 0, false);
		
		// default Value
		FUnknownPtr<IValue2> ivalue2 (value);
		if (ivalue2)
		{
			ivalue2->setDefault (state ? 1.f : 0.f);
			ivalue2->setValueFlag (IValue2::kIsAutomatable, automated);
		}

		if (controller)
			value->connect (controller, tag);
		values->add (new PValueEntry (value, name));
	}
	return value;
}

//------------------------------------------------------------------------
IValue* PValueContainer::addIntValue (int32 tag, FIDString name, int32 min, int32 max, int32 defvalue, bool automated /*= false*/, bool wrapAround /*= false*/)
{
	IValue* value = FHostCreate (IHostIntValue, host);
	if (value)
	{
		value->setMinValue (min);
		value->setMaxValue (max);
		value->setValue2 (defvalue, false);
		
		// default Value
		FUnknownPtr<IValue2> ivalue2 (value);
		if (ivalue2)
		{
			ivalue2->setDefault ((float)defvalue);
			ivalue2->setValueFlag (IValue2::kIsAutomatable, automated);
			ivalue2->setValueFlag (IValue2::kIsWrapAround, wrapAround);
		}

		if (controller)
			value->connect (controller, tag);
		values->add (new PValueEntry (value, name));
	}
	return value;
}

//------------------------------------------------------------------------
IValue* PValueContainer::addFloatValue (int32 tag, FIDString name, float min, float max, float defvalue, int32 precision /*= -1*/, bool automated /*= false*/, bool wrapAround /*= false*/)
{
	IValue* value = FHostCreate (IHostFloatValue, host);
	if (value)
	{
		initFloatValue (value, min, max, defvalue, precision, automated, wrapAround);

		if (controller)
			value->connect (controller, tag);

		values->add (new PValueEntry (value, name));
	}
	return value;
}

//------------------------------------------------------------------------
void PValueContainer::initFloatValue (IValue* value, float min, float max, float defvalue, int32 precision /*= -1*/, bool automated /*= false*/, bool wrapAround /*= false*/)
{
	value->setFloatMin (min);
	value->setFloatMax (max);
	value->setFloatValue (defvalue, false);

	// Precision
	FUnknownPtr<IFloatValue> fvalue (value);
	if (fvalue && precision >= 0)
		fvalue->setPrecision (precision);
	
	// default Value
	FUnknownPtr<IValue2> ivalue2 (value);
	if (ivalue2)
	{
		ivalue2->setDefault (defvalue);
		ivalue2->setValueFlag (IValue2::kIsAutomatable, automated);
		ivalue2->setValueFlag (IValue2::kIsWrapAround, wrapAround);
	}
}


//------------------------------------------------------------------------
IValue* PValueContainer::addStringValue (int32 tag, FIDString name, const tchar* text, bool automated)
{
	IValue* value = FHostCreate (IHostStringValue, host);
	if (value)
	{
		value->fromString2 (text, false);
		if (controller)
			value->connect (controller, tag);

		FUnknownPtr<IValue2> ivalue2 (value);
		if (ivalue2)
			ivalue2->setValueFlag (IValue2::kIsAutomatable, automated);

		values->add (new PValueEntry (value, name));
	}
	return value;
}

//------------------------------------------------------------------------
IValue* PValueContainer::addStringListValue (int32 tag, FIDString name, const tchar** items,
											 const tchar* selected, bool automated)
{
	IValue* value = FHostCreate (IHostStringListValue, host);
	if (value)
	{
		if (items)
		{
			FUnknownPtr <IStringList> strList (value);
			if (strList)
				strList->addStrings (items);
		}	

		if (selected)
			value->fromString2 (selected, false);
		else if (items && items[0])
			value->fromString2 (items[0], false);

		if (controller)
			value->connect (controller, tag);

		FUnknownPtr<IValue2> ivalue2 (value);
		if (ivalue2)
			ivalue2->setValueFlag (IValue2::kIsAutomatable, automated);

		values->add (new PValueEntry (value, name));
	}
	return value;
}

//------------------------------------------------------------------------
IValue* PValueContainer::getValueByIndex (int32 index)
{
	PValueEntry* entry = values->at (index);
	return entry ? entry->value : nullptr;
}

//------------------------------------------------------------------------
IValue* PValueContainer::getValueByTag (int32 tag)
{
	FOREACH_T (PValueEntry*, entry,(*values))
		if (entry->value && entry->value->getTag () == tag)
			return entry->value;
	ENDFOR
	return nullptr;
}

//------------------------------------------------------------------------
IValue* PValueContainer::getValue (FIDString name)
{
	FOREACH_T (PValueEntry*, entry,(*values))
		if (entry->name == name)
			return entry->value;
	ENDFOR
	return nullptr;
}

//------------------------------------------------------------------------
int32 PValueContainer::countValues ()
{
	return values->total ();
}

//------------------------------------------------------------------------
bool PValueContainer::getValueName (int32 index, char8 str[128])
{
	PValueEntry* entry = values->at (index);
	if (entry)
	{
		strcpy8 (str, entry->name);
		return true;
	}
	return false;
}

//------------------------------------------------------------------------
void PValueContainer::setValueActive (FIDString name, bool state)
{
	if (IValue* v = getValue (name))
		v->setActive (state);

}

//------------------------------------------------------------------------
void PValueContainer::setValueActive (int32 tag, bool state)
{
	if (IValue* v = getValueByTag (tag))
		v->setActive (state);
}

//------------------------------------------------------------------------
void PValueContainer::removeAll ()
{
	values->removeAndDeleteAll ();
}

}