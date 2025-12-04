// NamedPipe.cpp: implementation of the CNamedPipe class.
//
// Author:    Emil Gustafsson (e@ntier.se), 
//            NTier Solutions (www.ntier.se)
// Created:   2000-01-25
// Copyright: This code may be reused and/or editied in any project
//            as long as this original note (Author and Copyright)
//            remains in the source files.
//////////////////////////////////////////////////////////////////////

#include "NamedPipe.h"

#ifdef _DEBUG
#undef THIS_FILE
static char THIS_FILE[]=__FILE__;

#endif

#define PIPE_BUF_SIZE 1024
#define PIPE_TIMEOUT  (120*1000) /*120 seconds*/

//------------------------------------------------------------------------
CNamedPipe::CNamedPipe()
{
	m_szPipeName = "";
	m_szPipeHost = ".";
	m_szFullPipeName = "\\\\.\\PIPE\\";

	m_hOutPipe = NULL;
	m_hInPipe  = NULL;
}

//------------------------------------------------------------------------
CNamedPipe::~CNamedPipe()
{
	closePipe ();
}

//------------------------------------------------------------------------
bool CNamedPipe::initialize ()
{
	m_hInPipe = CreateNamedPipe (
		GetRealPipeName (true).c_str (),
		PIPE_ACCESS_INBOUND,
		PIPE_WAIT,
		1,
		PIPE_BUF_SIZE,
		PIPE_BUF_SIZE,
		PIPE_TIMEOUT,
		NULL);
	if (m_hInPipe == NULL || m_hInPipe == INVALID_HANDLE_VALUE)
		return false;

	m_hOutPipe = CreateNamedPipe (
		GetRealPipeName (false).c_str (),
		PIPE_ACCESS_OUTBOUND,
		PIPE_WAIT,
		1,
		PIPE_BUF_SIZE,
		PIPE_BUF_SIZE,
		PIPE_TIMEOUT,
		NULL);
	if (m_hOutPipe == NULL || m_hOutPipe == INVALID_HANDLE_VALUE)
		return false;

	return true;
}

//------------------------------------------------------------------------
void CNamedPipe::SetPipeName(string szName, string szHost/* = "."*/)
{
	m_szPipeName = szName;
	m_szPipeHost = szHost;
	m_szFullPipeName = "\\\\";
	m_szFullPipeName += m_szPipeHost;
	m_szFullPipeName += "\\PIPE\\";
	m_szFullPipeName += m_szPipeName;
}

//------------------------------------------------------------------------
string CNamedPipe::GetRealPipeName(bool bIsServerInPipe)
{
	string szRetVal = m_szFullPipeName;
	szRetVal += bIsServerInPipe?"_IN":"_OUT";
	return szRetVal;
}

//------------------------------------------------------------------------
bool CNamedPipe::read (string& szMsg /*out*/)
{
	char buf[PIPE_BUF_SIZE];
	DWORD dwRead = 1;
	
	BOOL bOK = ReadFile (m_hInPipe, buf, PIPE_BUF_SIZE, &dwRead, NULL);
	if (dwRead == 0 || !bOK)
		return false;
	
	szMsg = buf;	
	return true;
}

//------------------------------------------------------------------------
bool CNamedPipe::send (string szMsg)
{
	string copy = szMsg;
	DWORD dwSent;
	BOOL bOK = 0;
	try
	{
		bOK = WriteFile (m_hOutPipe, szMsg.c_str (), szMsg.length (), &dwSent, NULL);
	}
	catch (...)
	{
		throw copy;
	}

	// reset ... Otherwise BaseHead freezes
	closePipe ();
	initialize ();

	if (!bOK || (szMsg.length()+1) != dwSent)
		return false;
	return true;
}

//------------------------------------------------------------------------
void CNamedPipe::closePipe ()
{
	if (m_hOutPipe != NULL && m_hOutPipe != INVALID_HANDLE_VALUE)
		CloseHandle (m_hOutPipe);
	m_hOutPipe = NULL;

	if (m_hInPipe != NULL && m_hInPipe != INVALID_HANDLE_VALUE)
		CloseHandle (m_hInPipe);
	m_hInPipe;
} 
