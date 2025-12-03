
del /s /q /f \AISoundGenerator

IF EXIST AISoundGenerator.zip DEL /F AISoundGenerator.zip


#cd basehead

dotnet publish -p:PublishSingleFile=true -f net8.0 -r win-x64 -c Release -o AISoundGenerator --self-contained true

IF EXIST AISoundGenerator\AISoundGenerator.pdb DEL /F AISoundGenerator\AISoundGenerator.pdb
