#include "LogFile.h"

CLogFile::CLogFile(char *strFile, bool bAppend, long lTruncate)
{
	m_lTruncate = lTruncate;
	char	szFile[MAX_PATH + 1];
	if (strlen(strFile)>3 && strFile[1] != ':')	//no absolute path designated
	{
		::GetModuleFileNameA(NULL, szFile, MAX_PATH);
		int llength = strlen(szFile);
		char*	pcat = szFile + (llength - 1);	//point to the last char
		while (llength--)
		{
			pcat--;
			if (*pcat == '\\')
				break;
		}
		if (*pcat == '\\')
		{
			pcat++;
			strcpy(pcat, strFile);
		}
		else	//something wrong, use the original filename, ignore path problem
			strcpy(szFile, strFile);
	}
	else
		strcpy(szFile, strFile);

	strcpy(m_filename, szFile);

	m_pLogFile = fopen(m_filename, bAppend ? "a" : "w");
	if (m_pLogFile)
		fclose(m_pLogFile);

	InitializeCriticalSection(&m_cs);
}

void CLogFile::Write(char *pszFormat, ...)
{
	if (!m_pLogFile)
		return;

	EnterCriticalSection(&m_cs);
	m_pLogFile = fopen(m_filename, "a");

	//write the formated log string to szLog
	char	szLog[1024];
	va_list argList;
	va_start(argList, pszFormat);
	vsprintf(szLog, pszFormat, argList);
	va_end(argList);

	//Truncate if the file grow too large
	long	lLength = ftell(m_pLogFile);
	if (lLength > m_lTruncate)
		rewind(m_pLogFile);

	//Get current time
	SYSTEMTIME	time;
	::GetLocalTime(&time);
	TCHAR	szLine[2048];

	sprintf((char *)szLine, "%02d:%02d:%02d:%03d \t%s\n",
		time.wHour, time.wMinute, time.wSecond, time.wMilliseconds,
		szLog);

	fputs((char *)szLine, m_pLogFile);

	if (m_pLogFile)
		fclose(m_pLogFile);

	LeaveCriticalSection(&m_cs);
}
