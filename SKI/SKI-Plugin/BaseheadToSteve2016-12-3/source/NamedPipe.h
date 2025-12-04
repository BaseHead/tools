// NamedPipe.h: interface for the CNamedPipe class.
//
// Author:    Emil Gustafsson (e@ntier.se), 
//            NTier Solutions (www.ntier.se)
// Created:   2000-01-25
// Copyright: This code may be reused and/or editied in any project
//            as long as this original note (Author and Copyright)
//            remains in the source files.
//////////////////////////////////////////////////////////////////////

#if !defined(NAMEDPIPE_H)
#define NAMEDPIPE_H

#if _MSC_VER > 1000
#pragma once
#endif // _MSC_VER > 1000

#include <string>
#include <Windows.h>
using namespace std;


//------------------------------------------------------------------------
class CNamedPipe  
{
public:
	//--------------------------------------------------------------------
	CNamedPipe ();
	virtual ~CNamedPipe ();	

	bool initialize ();	

	void   SetPipeName (string szName, string szHost = ".");
	string GetPipeName () { return m_szFullPipeName; }
	string GetRealPipeName (bool bIsServerInPipe);

	bool read (string& szMsg /*out*/);
	bool send (string szMsg);

//------------------------------------------------------------------------
private:
	void closePipe ();

	string m_szPipeName;
	string m_szPipeHost;
	string m_szFullPipeName;

	HANDLE m_hInPipe;
	HANDLE m_hOutPipe;
};

#endif // !defined(NAMEDPIPE_H)
