#if !defined(CLIPBOARD_H)
#define CLIPBOARD_H

#if _MSC_VER > 1000
#pragma once
#endif // _MSC_VER > 1000

#include <Windows.h>
#include <string>

using namespace std;
class clipboard
{
public:
    void begin(HWND hwnd)
    {
        hWindow=hwnd;
    }

    void set(string str)
    {
        hString = ::GlobalAlloc(GMEM_MOVEABLE, sizeof(char) * (str.length() + 1));
        if (hString)
            if (::OpenClipboard (hWindow))
            {
                LPVOID szString = ::GlobalLock (hString);
                if (szString)
                {
                    memcpy(szString, str.c_str(), sizeof(char) * (str.length() + 1));
                    ::GlobalUnlock (hString);
                    ::EmptyClipboard();
                    ::SetClipboardData(CF_TEXT, hString);
                }
                ::CloseClipboard();
            }
    }
    
	string get()
    {
        string str;
        if (::OpenClipboard (hWindow))
                {
                    hString = ::GetClipboardData(CF_TEXT);
                    if (hString)
                    {
                        str = (char*)::GlobalLock(hString);
                        ::GlobalUnlock(hString);
                    }
                }
                ::CloseClipboard();
        return str;
    }
    
	bool is_available()
    {
        return IsClipboardFormatAvailable(CF_TEXT);
    }

protected:
    HGLOBAL hString;
    HWND hWindow;
};

#endif