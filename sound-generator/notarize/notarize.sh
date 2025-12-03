set -e

codesign --deep --force --verbose --entitlements "notarize/basehead.entitlements" --options=runtime --sign "Developer ID Application: Steve Tushar (WVJ3GT3TVW)" "notarize/build/arm64/AISoundGenerator-arm64.app"
spctl -a -t exec -vv "notarize/build/arm64/AISoundGenerator-arm64.app"

codesign --deep --force --verbose --entitlements "notarize/basehead.entitlements" --options=runtime --sign "Developer ID Application: Steve Tushar (WVJ3GT3TVW)" "notarize/build/x64/AISoundGenerator.app"
spctl -a -t exec -vv "notarize/build/x64/AISoundGenerator.app"

chmod a+x ./notarize/notarize.py
./notarize/notarize.py -u steve@baseheadinc.com -p deft-pzrm-dssx-rslx -t WVJ3GT3TVW --asc-provider steve@baseheadinc.com --file notarize/build/arm64/AISoundGenerator-arm64.app
./notarize/notarize.py -u steve@baseheadinc.com -p deft-pzrm-dssx-rslx -t WVJ3GT3TVW --asc-provider steve@baseheadinc.com --file notarize/build/x64/AISoundGenerator.app


#Intel
#productbuild --component "/Users/steve/Desktop/BitBucket/basehead/notarize/build/x64/basehead.app" /Applications "/Users/steve/Desktop/BitBucket/basehead/notarize/build/x64/basehead.pkg" --sign "Developer ID installer: Steve Tushar (WVJ3GT3TVW)"
#productsign --sign "Developer ID installer: Steve Tushar (WVJ3GT3TVW)" "/Users/steve/Desktop/BitBucket/basehead/notarize/build/x64/basehead.pkg" "/Users/steve/Desktop/BitBucket/basehead/notarize/build/x64/basehead.pkg-signed"
#mv "/Users/steve/Desktop/BitBucket/basehead/notarize/build/x64/basehead.pkg-signed" "/Users/steve/Desktop/BitBucket/basehead/notarize/build/x64/basehead.pkg"#


#ARM64
#productbuild --component "/Users/steve/Desktop/BitBucket/basehead/notarize/build/arm64/basehead.app" /Applications "/Users/steve/Desktop/BitBucket/basehead/notarize/build/arm64/basehead-arm64.pkg" --sign "Developer ID installer: Steve Tushar (WVJ3GT3TVW)"
#productsign --sign "Developer ID installer: Steve Tushar (WVJ3GT3TVW)" "/Users/steve/Desktop/BitBucket/basehead/notarize/build/arm64/basehead-arm64.pkg" "/Users/steve/Desktop/BitBucket/basehead/notarize/build/arm64/basehead-arm64.pkg-signed"
#mv "/Users/steve/Desktop/BitBucket/basehead/notarize/build/arm64/basehead-arm64.pkg-signed" "/Users/steve/Desktop/BitBucket/basehead/notarize/build/arm64/basehead-arm64.pkg"



## Build the installer package
#/usr/local/bin/packagesbuild "Installer/basehead v2025.pkgproj"

## Sign the installer package
#productsign --sign "Developer ID Installer: Steve Tushar (WVJ3GT3TVW)" "Installer/build/Install basehead v2025.pkg" "Installer/build/Install basehead v2025-signed.pkg"
#mv "Installer/build/Install basehead v2025-signed.pkg" "Installer/build/Install basehead v2025.pkg"

## Notarize installer package
## Notarize installer package
#chmod a+x ./notarize/notarize.py
#./notarize/notarize.py -u steve@baseheadinc.com -p deft-pzrm-dssx-rslx -t WVJ3GT3TVW --asc-provider steve@baseheadinc.com --file "Installer/build/Install basehead v2025.pkg"

## Nuke the Release build folder
#rm -f -r ../baseheadconnect/BaseHead\ Connect/Builds/MacOSX/Build/Release
