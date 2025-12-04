#ifndef _ATA_LOGFILE_
#define _ATA_LOGFILE_

#define _DEBUG_LOG

#include <string>
#include <Windows.h>

using namespace std;

class CLogFile
{
public:
	CLogFile(char *strFile, bool bAppend = FALSE, long lTruncate = 4096);
	void Write(char *pszFormat, ...);
	virtual ~CLogFile() { DeleteCriticalSection(&m_cs); };
private:
	FILE*	m_pLogFile;
	long	m_lTruncate;
	CRITICAL_SECTION	m_cs;
	char m_filename[2 * MAX_PATH + 1];
};

#endif //_ATA_LOGFILE_
