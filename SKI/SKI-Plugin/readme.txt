For compilation used VC++ 10

UPDATE: USE SDK 4.1 INSTEAD NOW!!
1) Place Steinberg SDK in c:\SKI3_1 SDK\ folder
2) Unpack archive Basehead.rar to C:\SKI3_1 SDK\public.sdk\samples\BaseHead
3) Open in VC++ 10 solution file BaseheadSKI.sln (C:\SKI3_1 SDK\public.sdk\samples\Basehead\win)
4) Compile

I used only STL/standard C++ library for absence in dependecies
from external libraries

5) For Windows 32 bit:
   Place compiled baseheadSKI.dll in 
   $Program Files\Common Files\Steinberg\Shared components\Basehead
   folder (or $Program Files (x86) for x64 Windows and 32 bits Nuendo)

   For Windows 64 bit and Nuendo 5.5. 64 bits:
   Place baseheadSKIx64.dll in 
   $Program Files\Steinberg\Nuendo 5.x\Components

6) Start BH3, choose existing audio file and press 'S' :-)