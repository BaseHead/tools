#set -e



echo ".NET build"
dotnet publish sound-generator/AISoundGenerator.sln -c Release -f net8.0 -r osx-x64 -o publish-x64 --self-contained true
dotnet publish sound-generator/AISoundGenerator.sln -c Release -f net8.0 -r osx-arm64 -o publish-arm64 --self-contained true




echo "ARM: Prepare directories"
APP_NAME="AISoundGenerator-arm64.app"
PUBLISH_OUTPUT_DIRECTORY="./publish-arm64/."
INFO_PLIST="infodotPlist/as/Info.plist"
ICON_FILE="basehead.icns"
if [ -d "$APP_NAME" ]
then
    rm -rf "$APP_NAME"
fi
mkdir "$APP_NAME"
mkdir "$APP_NAME/Contents"
mkdir "$APP_NAME/Contents/MacOS"
mkdir "$APP_NAME/Contents/Resources"

echo "ARM: Copy application resources"
cp -v "$INFO_PLIST" "$APP_NAME/Contents/Info.plist"
cp -v "$ICON_FILE" "$APP_NAME/Contents/Resources/basehead.icns"
cp -v -R "$PUBLISH_OUTPUT_DIRECTORY" "$APP_NAME/Contents/MacOS"

echo "X86: Prepare directories"
APP_NAME="AISoundGenerator.app"
PUBLISH_OUTPUT_DIRECTORY="./publish-x64/."
INFO_PLIST="infodotPlist/intel/Info.plist"
ICON_FILE="basehead.icns"
if [ -d "$APP_NAME" ]
then
    rm -rf "$APP_NAME"
fi
mkdir "$APP_NAME"
mkdir "$APP_NAME/Contents"
mkdir "$APP_NAME/Contents/MacOS"
mkdir "$APP_NAME/Contents/Resources"

echo "X86: Copy application resources"
cp -v "$INFO_PLIST" "$APP_NAME/Contents/Info.plist"
cp -v "$ICON_FILE" "$APP_NAME/Contents/Resources/basehead.icns"
cp -v -R "$PUBLISH_OUTPUT_DIRECTORY" "$APP_NAME/Contents/MacOS"

rm -rf notarize/build
mkdir notarize/build/
mkdir notarize/build/x64/
mkdir notarize/build/arm64/

echo "Copy applications binaries to notarize build"
cp -v -r AISoundGenerator.app  notarize/build/x64/AISoundGenerator.app
cp -v -r AISoundGenerator-arm64.app  notarize/build/arm64/AISoundGenerator-arm64.app

rm -rf AISoundGenerator.app
rm -rf AISoundGenerator-arm64.app



