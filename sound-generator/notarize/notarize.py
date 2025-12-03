#!/usr/bin/env python3

"""Script that automates the Apple notarization process."""

################################################################################
#
#	File:		notarize.py
#
#	Contains:	This script uses Apple's command line tools to notarize the
#				specified package. Since this is an async process, this script
#				will sleep for 30 seconds after posting to Apple waiting for
#				the result. When the notarization succeeds the posted package
#				will be stapled.
#
#				This script is tolerant of errors resulting from trying to
#				post the same binary multiple times because that can be
#				helpful for triage builds.
#
#	Usage:		For usage details see the help text:
#
#				notarize.py --help
#
#	Copyright:	2019 - 2023 by PACE Anti-Piracy, all rights reserved.
# 
################################################################################

# Python 2 and 3 compatibility
from __future__ import absolute_import
from __future__ import division
from __future__ import print_function

import os
import errno
import sys
import stat
import platform
import time
import subprocess
import shutil
import json
import re
import plistlib
import tempfile
from optparse import OptionParser

# Globals
gDebug = False
gQuiet = False
gSleepSeconds = 30
gLogFile = None

gSeparator		= "=========================================================================================="
gSeparatorMinor = "------------------------------------------------------------------------------------------"

# The stapler errors with frameworks and zip files.
gStapleUnsupportedExtensions = [".framework", ".zip"]

# Environment variable that disables notarization
gDisableNotarizationEnvVarName = "PACE_EDEN_NOTARIZE_DISABLE"

# List of boolean string equivalents
gBooleanTrueStrings = ['yes', 'true', 'on', '1']

# Get the name and path to our script
scriptPathArg = sys.argv[0]
scriptName = os.path.basename(scriptPathArg)
scriptDir = os.path.realpath(os.path.dirname(scriptPathArg))

# Global path to notarytool
gNotarytoolPath = None

#-----------------------------------------------------------------------------------------
# Get the default string encoding for this script. If the environment isn't configured or
# if any exception is caught, then set the default encoding to ASCII.
try:
	gDefaultEncoding = locale.getdefaultlocale()[1]
	if (gDefaultEncoding == None) or (gDefaultEncoding == ""):
		gDefaultEncoding = 'ascii'
except:
	#print("Setting the default encoding to ASCII because we caught an exception accessing the default locale!")
	gDefaultEncoding = 'ascii'
#p("gDefaultEncoding: " + gDefaultEncoding)

#-----------------------------------------------------------------------------------------
def printLeader():
	return scriptName + ": "
	
#-----------------------------------------------------------------------------------------
def p(*args):
	"Print to stdout with standard print leader string"
	line = printLeader()
	for arg in args:
		line = " ".join([line, str(arg)])
	if gLogFile:
		gLogFile.write(line + "\n")
	else:
		print(line)
	return

#-----------------------------------------------------------------------------------------
def eprint(*args, **kwargs):
	if gLogFile:
		fileToUse = gLogFile
	else:
		fileToUse = sys.stderr
	print(*args, file=fileToUse, **kwargs)

#-----------------------------------------------------------------------------------------
def e(*args):
	"Print to stderr with standard print leader string"
	line = printLeader()
	for arg in args:
		line = " ".join([line, str(arg)])
	eprint(line)
	return

#-----------------------------------------------------------------------------------------
def e1(*args):
	"Print first line of error output that includes ERROR: after print leader"
	e("ERROR:", *args)
	return

#-----------------------------------------------------------------------------------------
def exitFail(exitCode=1, quiet=True):
	"Exit script with failure code and message."
	if not quiet: 
		e("**** Failed with exit code:", exitCode)
		
	if not gQuiet:
		sayIt("The notarize operation failed.", inError=True)

	sys.exit(exitCode)

#-----------------------------------------------------------------------------------------
def exitSuccess(exitCode=0, quiet=True):
	"Exit script with success exit code and message"
	if not quiet: p("Completed successfully")
	
	if not gQuiet:
		sayIt("The notarize operation succeeded.", inError=False)
	
	sys.exit(exitCode)

#------------------------------------------------------------------------------------------------------
def stringArgCheck(arg):
	return (arg		!= None)	and \
		   (len(arg) > 0   )	and \
		   (arg		!= "")		and \
		   (arg		!= '')		and \
		   (arg		!= "\"\"")

#-----------------------------------------------------------------------------------------
def removeEnclosingQuotes(quotedStr):
	returnStr = quotedStr
	returnLen = len(returnStr)
	if returnStr.startswith("\"") or returnStr.startswith("'"):
		returnStr = returnStr[1:]
		returnLen = len(returnStr)
		if returnStr.endswith("\"") or returnStr.endswith("'"):
			returnStr = returnStr[:returnLen-1]
	return returnStr

#------------------------------------------------------------------------------------------------------
def validatePathOption(path, optionName, mustExist=False):
	path = removeEnclosingQuotes(path)
	doesExist = False
	if not stringArgCheck(path):
		e1("Invalid " + optionName + " path:", path)
		exitFail()
	abspath = os.path.abspath(path)
	doesExist = os.path.exists(abspath)
	if mustExist and not doesExist:
		e1("Required " + optionName + " path does not exist:", path)
		exitFail()
	return doesExist

#------------------------------------------------------------------------------------------------------
def validateFileOption(path, optionName, mustExist=False):
	if not stringArgCheck(path):
		e1("Invalid " + optionName + " path:", path)
		exitFail()
	path = removeEnclosingQuotes(path)
	doesExist = validatePathOption(path, optionName, mustExist)
	if doesExist and not os.path.isfile(path):
		e1("The specified existing" + optionName + " is not a file:", path)
		exitFail()
	return doesExist

#------------------------------------------------------------------------------------------------------
def validateDirectoryOption(path, optionName, mustExist=False):
	if not stringArgCheck(path):
		e1("Invalid " + optionName + " path:", path)
		exitFail()
	path = removeEnclosingQuotes(path)
	doesExist = validatePathOption(path, optionName, mustExist)
	if doesExist and not os.path.isdir(path):
		e1("The specified existing" + optionName + " is not a directory:", path)
		exitFail()
	return doesExist

#-----------------------------------------------------------------------------------------
# Return true if the specified path is to a real file, not a symlink or a
# directory.
def isExistingRealFile(inPath):
	if os.path.exists(inPath) and not os.path.islink(inPath) and os.path.isfile(inPath):
		return True
	
	return False

#-----------------------------------------------------------------------------------------
# Return true if the specified path is to a real directory, not a symlink or a
# directory.
def isExistingRealDir(inPath):
	if os.path.exists(inPath) and not os.path.islink(inPath) and os.path.isdir(inPath):
		return True
	
	return False

#-----------------------------------------------------------------------------------------
# This is needed on Windows to remove read only git files
def handleRemoveReadonly(func, path, exc):
  excvalue = exc[1]
  if func in (os.rmdir, os.remove) and excvalue.errno == errno.EACCES:
	  os.chmod(path, stat.S_IRWXU| stat.S_IRWXG| stat.S_IRWXO) # 0777
	  func(path)
  else:
	  raise

#----------------------------------------------------------------------
def remove(path):
	"""
	Remove the file or directory
	"""
	if os.path.isdir(path):
		try:
			shutil.rmtree(path, ignore_errors=False, onerror=handleRemoveReadonly)
		except OSError:
			p("Unable to remove folder: " + path)
			raise
	else:
		try:
			if os.path.exists(path):
				os.remove(path)
		except OSError:
			p("Unable to remove file: " + path)
			raise

#-----------------------------------------------------------------------------------------
# If the specified directory exists, then remove it.
def rmDirIfExists(inDirToRemove, inMessage=None):
	didRemove = False
	
	if isExistingRealDir(inDirToRemove):
		if inMessage:
			p(inMessage)
		elif gDebug:
			p("Removing this directory: \"" + inDirToRemove + "\"")
		shutil.rmtree(inDirToRemove, ignore_errors=False, onerror=handleRemoveReadonly)
		didRemove = True
	
	return didRemove

#---------------------------------------------------------------------------
# This is the equivalent of 'mkdir -p', where we ignore the error if the
# path exists
def mkdir_p(path):
	try:
		os.makedirs(path)
	except OSError as exc:
		if exc.errno == errno.EEXIST:
			pass
		else: raise
	return

#---------------------------------------------------------------------------
# Convert the incoming seconds in floating point to a human readable string.
def humanReadableSeconds(inSeconds, inMessage="", inIncludeSeconds=False):
	message = inMessage
	if inSeconds > 0.0:
		#print("seconds = ", inSeconds)
		
		years = int(inSeconds / 31536000)
		days = int((inSeconds % 31536000) / 86400)
		hours = int(((inSeconds % 31536000) % 86400) / 3600)
		minutes = int((((inSeconds % 31536000) % 86400) % 3600) / 60)
		seconds = int((((inSeconds % 31536000) % 86400) % 3600) % 60)
		elapsedLong = int(inSeconds)
		if elapsedLong:
			ms = int((inSeconds - float(elapsedLong)) * 1000)
		else:
			ms = int(inSeconds * 1000)
		
		if years:
			unitStr = " year"
			if years > 1:
				unitStr += "s"
			message = message + str(years) + unitStr + " "

		if days:
			unitStr = " day"
			if days > 1:
				unitStr += "s"
			message = message + str(days) + unitStr + " "

		if hours:
			unitStr = " hour"
			if hours > 1:
				unitStr += "s"
			message = message + str(hours) + unitStr + " "

		if minutes:
			unitStr = " minute"
			if minutes > 1:
				unitStr += "s"
			message = message + str(minutes) + unitStr + " "

		if seconds:
			unitStr = " second"
			if seconds > 1:
				unitStr += "s"
			message = message + str(seconds) + unitStr + " "

		if ms:
			unitStr = " ms"
			message = message + str(ms) + unitStr + " "
		
		if inIncludeSeconds:
			message = message + "(" + str(inSeconds) + " seconds) "
	else:
		message = message + str(inSeconds) + " seconds "
	
	return message

#-----------------------------------------------------------------------------------------
# Take the specified object and convert it to a string in a safe way, catching any 
# encoding errors and ignoring characters that do not comply to the specified encoding
# (which defaults to the encoding associated with the current locale).
#
# Warning! Since this function will drop any characters that have encoding that is not 
# understood, you should only use this in cases where you  have no control over the
# encoding of the incoming data (like the output of a subprocess command line tool) 
# and/or don't care about dropping characters.
#
# Borrowed from here:
#
#	https://stackoverflow.com/questions/9942594/unicodeencodeerror-ascii-codec-cant-encode-character-u-xa0-in-position-20
#
def safeString(obj, encoding=gDefaultEncoding):
	try: 
		return str(obj)
	except UnicodeEncodeError:
		return obj.encode(encoding, 'ignore').decode(encoding)
	return ""

#-----------------------------------------------------------------------------------------
# Convert bytes to a string, ignoring any Unicode exceptions. Uses the encoding indicated
# by the current locale by default.
def safeBytesToString(bytes, encoding=gDefaultEncoding):
	return safeString(bytes.decode(encoding, 'ignore'), encoding)

#-----------------------------------------------------------------------------------------
def runSyncCommand(cmd, stripLineEnds=True, printOutput=False, captureOutput=True, encoding='ascii'):
	"""Run a command synchronously in a shell. Return the command exit code and its 
	output, captured in a list. If captureOutput is False, then the subprocess will just 
	inherit the stdout file handle and we won't see the output at all, so printOutput is 
	effectively True and the returned list will be empty. If captureOutput is True, then
	we will capture the output into a list, and the output will only be displayed if printOutput
	is True."""
	global gDebug
	
	outputList=[]
	stdoutArg = None
	if captureOutput:
		stdoutArg = subprocess.PIPE
	
	if sys.platform == "darwin":
		cmd = "set PYTHONUNBUFFERED=\"yes\"; export PYTHONUNBUFFERED; " + cmd
	elif sys.platform == "win32":
		cmd = "set PYTHONUNBUFFERED=yes & " + cmd

	if gDebug:
		p("Command line: ")
		print(cmd)
	c = subprocess.Popen(cmd, shell=True, 
						 stdout=stdoutArg, stderr=subprocess.STDOUT)

	if captureOutput:
		if gDebug:
			p("Enumerating lines in c.stdout")
		for lineBytes in c.stdout:
			line = safeBytesToString(lineBytes, encoding)
			if printOutput:
				print(line, end='')
			outputList.append((line.rstrip(os.linesep) if stripLineEnds else line[:]))
			
	# Wait for command completion
	if gDebug:
		p("Wait for command completion")
	result = c.wait()

	return (result, outputList)

#-----------------------------------------------------------------------------------------
def runCommandAndExitOnError(cmd, errorMessage, printOutput=True):
	global gDebug

	(result, outputList) = runSyncCommand(cmd, printOutput=printOutput)
	if result != 0:
		# Display the error output to the caller
		for line in outputList:
			p(line)
		if gDebug:
			errorMessage = errorMessage + " " + cmd
		e1(errorMessage)
		exitFail()
	
	return (result, outputList)
	
#------------------------------------------------------------------------------------------------------
# Return the value for the specified environment variable, suppressing exceptions. If the
# environment variable is not found, and empty string is returned.
def getEnvVarValue(envVarName):
	# Get the environment variable, suppressing exceptions
	envVarValue = ""
	try:
		envVarValue = os.environ[envVarName]
	except KeyError:
		pass
	
	return envVarValue

#-----------------------------------------------------------------------------------------
# For Mac, use the "say" command to provide the caller with an audible message.
def sayIt(inMessage, inError=False):
	cmd = "say " + inMessage
	runSyncCommand(cmd, captureOutput=False)

#-----------------------------------------------------------------------------------------
# Wrapper function to read a plist from a string that works with both python 2 and 3.
def readPlistFromString(plistStr):
	# Find the beginning of the XML (ignoring any warnings or other possible preceding
	# text).
	if gDebug:
		p("plistStr:", str(plistStr))
	xmlStartIndex = plistStr.find("<?xml")
	if xmlStartIndex == -1:
		raise Exception("Can't parse this plist string because we can't find the start of the XML: " + str(plistStr))
	xmlStr = plistStr[xmlStartIndex:]
	
	# Parse the plist XML
	if sys.version_info.major > 2:
		if gDebug:
			p("xmlStr:", str(xmlStr))
			p("xmlStr.encode():", str(xmlStr.encode()))
		plist = plistlib.loads(xmlStr.encode())
	else:
		plist = plistlib.readPlistFromString(xmlStr)
	if gDebug:
		p("readPlistFromString plist:", str(plist))
		
	return plist

#-----------------------------------------------------------------------------------------
# Wrapper function to read a plist from a file that works with both python 2 and 3.
def readPlistFromFile(plistFilePath):
	if sys.version_info.major > 2:
		with open(plistFilePath, 'rb') as fp:
			plist = plistlib.load(fp)
	else:
		plist = plistlib.readPlist(plistFilePath)
	if gDebug:
		p("readPlistFromFile plist:", str(plist))
		
	return plist

#-----------------------------------------------------------------------------------------
def findAndParsePlistDataFromCommandOutout(outputList):
	global gDebug

	# Parse the resulting plist, even if we got an error. We have to search the output
	# for the start of the xml because it's possible that stderr output is also in the
	# outputList provided.
	errorOutput = []
	plistOutput = []
	foundStartOfPList = False
	for line in outputList:
		if foundStartOfPList:
			plistOutput.append(line)
		else:
			if line.startswith("<?xml"):
				foundStartOfPList = True
				plistOutput.append(line)
			else:
				errorOutput.append(line)
	
	# If we didn't get a plist, then all we got was error info. Dump it and exit.
	if len(plistOutput) == 0:
		for line in errorOutput:
			p(errorOutput)
		exitFail()
	
	# Parse the plist data provided
	plistStr = " ".join(plistOutput)
	plist = readPlistFromString(plistStr)
		
	return plist

#-----------------------------------------------------------------------------------------
# Returns a path string if notarytool exists on this system. If notarytool does not exist,
# return an empty string
def getNotarytoolPath():
	global gNotarytoolPath
	
	# If we haven't asked for the path to notarytool yet, then do so now. If we find it,
	# then remember the path in our global.
	#
	# If we get an error, then notarytool is not installed on this system, so we set our
	# global to an empty string. This way the next time we're asked for notarytool we'll
	# just return the empty string instead of trying to look for it again.
	if not gNotarytoolPath:
		cmd = 'xcrun --find notarytool'
		(result, outputList) = runSyncCommand(cmd, printOutput=False)
		if result == 0:
			gNotarytoolPath = outputList[0]
		else:
			gNotarytoolPath = ""
	
	return gNotarytoolPath
	
#-----------------------------------------------------------------------------------------
def uploadPackage(package, username, password, asc_provider, primary_bundle_id, useNotarytool, team_id, keychain_profile):
	global gDebug

	uuid = None
	alreadyUploaded = False
	
	p("Uploading this package to Apple: " + package)
	if useNotarytool:
		# Initialize the notarytool command. Since its normal progress output is kind of nice
		# and since our need to parse the resulting plist should be reduced (because it's a
		# better tool) we do not suppress progress or specify plist output.
		cmd = '"' + getNotarytoolPath() + '" submit "' + package + '" --wait'
		if keychain_profile:
			cmd += ' --keychain-profile "' + keychain_profile + '"'
		else:
			cmd += ' --apple-id "' + username + '" --team-id "' + team_id + '"'
			if password:
				cmd += ' --password "' + password + '"'
		
		# If we're debugging, tell notarytool to give us debug output
		if gDebug:
			cmd += ' --verbose'
		
		# Add a line feed so that the first line from the tool is separated from our usual
		# notarize.py output. This is just for aesthetics.
		print()
	else:
		cmd = 'xcrun altool --notarize-app -t osx --output-format xml --file "' + package + '" --primary-bundle-id ' + primary_bundle_id + ' --username ' + username + ' --password "' + password + '"'
		if asc_provider:
			cmd = cmd + " --asc-provider " + asc_provider
	(result, outputList) = runSyncCommand(cmd, printOutput=useNotarytool)
	
	# Deal with the output depending on whether we're using notarytool or altool
	if useNotarytool:
		# If we got an error result construct an error message from it
		if result != 0:
			errorMessage = 'Error result ' + str(result) + '.'

		# Loop through the output text looking for the prefix string indicating the
		# submission ID. While we don't really need this when using notarytool, this
		# function is supposed to return it, so we make a best effort to extract it here.
		for curLine in outputList:
			line = curLine.strip()
			if line.startswith('id:'):
				uuid = line.split()[1]
				break
		
		if not uuid:
			p("Warning: We could not parse the notarytool output for the submission ID.")
			uuid = "UNKNOWN"
	else:
		# Parse the resulting plist, even if we got an error.
		plist = findAndParsePlistDataFromCommandOutout(outputList)

		# If we got an error double check it to see if the issue is that we've already uploaded
		# the package. If that's the case, it's not a fatal error. Being tolerant of this makes
		# triage builds less difficult.
		if result != 0:
			productErrorsDict = plist['product-errors'][0]
			if productErrorsDict:
				errorCode = int(productErrorsDict['code'])
				errorMessage = productErrorsDict['message']
				if errorCode == -18000:
					if gDebug:
						p(errorMessage)
					p("The notarization service says this package has already been uploaded. This is a non-fatal error.")
			
					# Try to recover the uuid from the error text. Example:
					#
					#	The upload ID is 09039bec-35e9-4cf2-af2f-30a64ef33190"
					matches = re.findall("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", errorMessage)
					if len(matches) > 0:
						# If we found the UUID, then forget that we had an error
						uuid = matches[0]
						result = 0
						alreadyUploaded = True
					else:
						p("What? We got an error code recognize, but could not extract the upload UUID. Maybe Apple has changed their error format?")
						p("Here's the error message we tried to parse for the UUID:")
						p(errorMessage)
	
	# If we got an error, then show the output and error out
	if result != 0:
		# Display the error output to the caller
		for line in outputList:
			p(line)
		if gDebug:
			errorMessage = errorMessage + " " + cmd
		e1(errorMessage)
		exitFail()

	if useNotarytool:
		p("Package submission complete. Submission ID: ", uuid)
	else:
		# If we don't have a UUID yet it's because we successfully posted and need to retrieve
		# the UUID from the resulting plist returned.
		if not uuid:
			uuid = plist['notarization-upload']['RequestUUID']
			p("Package successfully uploaded. Job UUID:", uuid)
		else:
			p("Job UUID of already uploaded package:", uuid)

	return (uuid, alreadyUploaded)

#-----------------------------------------------------------------------------------------
# Check the status of a notarization request by UUID. If an error has happened, this
# function will exit with an error. Otherwise this function will return True if the
# notarization is still in progress, or False if it completed successfully.
def checkStatus(uuid, username, password):
	global gDebug

	p("Asking Apple for notarization status...")
	cmd = 'xcrun altool --notarization-info ' + uuid + ' --username ' + username + ' --password ' + password + ' --output-format xml'
	(result, outputList) = runSyncCommand(cmd, printOutput=False)

	# Parse the resulting plist
	plistStr = " ".join(outputList)
	plist = readPlistFromString(plistStr)
	
	# Apple has a bug where it can provide us with a Job UUID, but when we ask for status
	# it tells us that it the ID is unknown. We have to ignore this error and allow
	# retries to happen.
	retryNeeded = False
	if result != 0:
		productErrorsDict = plist['product-errors'][0]
		if productErrorsDict:
			errorCode = int(productErrorsDict['code'])
			errorMessage = productErrorsDict['message']
			if errorCode == 1519:
				if gDebug:
					p(errorMessage)
				p("The notarization service said that it can't find the UUID that it just gave us. We assume this is an Apple Bug and will ignore the error...")
				retryNeeded = True
			else:
				# We got an unexpected error from Apple's service.
				# Display the error output to the caller
				for line in outputList:
					p(line)
				if gDebug:
					errorMessage = errorMessage + " " + cmd
				e1(errorMessage)
				e1("Error getting notarization status.")
				exitFail()

	# If we need to retry due to an Apple bug, then we'll tell the caller that the operation
	# is still in progress. Otherwise we look at the status to see if it looks like the
	# operation is still in progress.
	if retryNeeded:
		inProgress = True
	else:
		# Get the status from the plist
		status = plist['notarization-info']['Status']
		if gDebug:
			p("status:", status)
	
		# See if the operation is still in progress or if it succeeded
		inProgress = 'in progress' in status
		success = 'success' in status
		if not inProgress and not success:
			for line in outputList:
				p(line)
			e1("Notarization failed!")
			exitFail()

	return inProgress

#-----------------------------------------------------------------------------------------
def waitTillNotarizationDone(uuid, username, password):
	# Note that we wait up front because I saw a failure when asking for progress immediately
	# after uploading a package. Apparently Apple needs a bit of time between uploading
	# and making the first status request. Ugly...
	while True:
		p('Notarization in progress. Checking its status in ' + str(gSleepSeconds) + ' seconds...')
		time.sleep(gSleepSeconds)
		if not checkStatus(uuid, username, password):
			break

#-----------------------------------------------------------------------------------------
def copyPackageToOutputDirectory(outdir, package):
	# Create the output directory if it doesn't exist
	mkdir_p(outdir)
	
	# Define the path to the destination
	packageName = os.path.basename(package)
	destPackage = os.path.join(outdir, packageName)

	# Remove the destination package if it exists in order to get it out of the way
	# of the copy
	if os.path.exists(destPackage):
		p("Deleting existing destination package...")
		remove(destPackage)

	# Copy the posted package to the destination folder
	p("Copying " + packageName + " to the destination...")
	#shutil.copy(package, outdir)
	destPath = outdir
	if os.path.isdir(package):
		destPath = destPackage
	cmd = 'ditto "' + package + '" "' + destPath + '"'
	(result, outputList) = runCommandAndExitOnError(cmd, "Error copying the package to the output directory.", printOutput=False)
	
	return destPackage

#-----------------------------------------------------------------------------------------
def attemptToStaplePackageOnce(package):
	p("Stapling this package: " + package)
	cmd = 'xcrun stapler staple "' + package + '"'
	(result, outputList) = runSyncCommand(cmd, printOutput=False)
	
	return result

#-----------------------------------------------------------------------------------------
def staplePackage(package):
	while True:
		if not attemptToStaplePackageOnce(package):
			break
		# This typically works the first time, but wait a little while and try again.
		p('Waiting for Apple server-side systems to sync. Retrying in ' + str(gSleepSeconds) + ' seconds...')
		time.sleep(gSleepSeconds)

#-----------------------------------------------------------------------------------------
def showExamples():
	exampleString = \
		"Examples of notarize.py usage:\n"\
		"\n"\
		"Save your notarization credentials to the keychain under the name 'notarization' using Apple's\n"\
		"notarytool. You will be prompted for your notarization application password. This is a one time\n"\
		"per system operation that will allow notarization operations to work securely in the future\n"\
		"without needing to provide your password again:\n"\
		"\n"\
		"  xcrun notarytool store-credentials notarization --apple-id johndoe@mycompany.com --team-id TBLAHBLAH\n"\
		"\n"\
		"---------------------------------------------------------------------------------------------------\n\n"\
		"Notarize an application using Apple's notarytool, where the notarization credentials have been\n"\
		"stored in the keychain using notarytool's 'store-credentials' option under the name 'notarization'.\n"\
		"The stapled result will be copied to a separate output directory:\n"\
		"\n"\
		"  notarize.py --keychain-profile notarization --file MyFavoriteApp.app --outdir build/notarized\n"\
		"\n"\
		"---------------------------------------------------------------------------------------------------\n\n"\
		"Notarize an installer package using Apple's notarytool, where the input pkg file will be stapled in\n"\
		"place:\n"\
		"\n"\
		"  notarize.py --keychain-profile notarization -f MyFavoriteApp.pkg\n"\
		"\n"\
		"---------------------------------------------------------------------------------------------------\n\n"\
		"Notarize an application using Apple's notarytool specifying the Apple ID and team ID. Since the\n"\
		"keychain profile has not been provided, the notarization process will stop and wait for you to\n"\
		"manually enter your password. Once authenticated, the process should complete and will be stapled\n"\
		"in place.\n"\
		"\n"\
		"  notarize.py --username johndoe@mycompany.com --team-id TBLAHBLAH --file MyFavoriteApp.app\n"\
		"\n"\
		"---------------------------------------------------------------------------------------------------\n\n"\
		"Notarize an application using the Apple deprecated altool, where the notarization application\n"\
		"password has been stored in the keychain under the name 'notarization', allowing this script to\n"\
		"use the application's bundle ID as the primary bundle ID for notarization, and copying the\n"\
		"stapled result to a separate output directory:\n"\
		"\n"\
		"  notarize.py --username johndoe@mycompany.com --password @keychain:notarization --file MyFavoriteApp.app --outdir build/notarized\n"\
		"\n"\
		"---------------------------------------------------------------------------------------------------\n\n"\
		"Notarize a disk image file using Apple's deprecated altool, saving the stapled result in a separate\n"\
		"output directory:\n"\
		"\n"\
		"  notarize.py -u johndoe@mycompany.com -p @keychain:notarization --primary-bundle-id com.mycompany.dmg.MyFavoriteApp -f MyFavoriteApp.dmg -o build/notarized\n"\
		"\n"

	p(exampleString)

#-----------------------------------------------------------------------------------------
gDescription="""\
This script calls Apple's command line tools to notarize software in a synchronous fashion. 
This script will first upload the package to be notarized, then it will loop until the notarization operation succeeds or fails.
If the upload and notarization is successful, the package will be stapled (if it's a format that supports stapling).
In addition to dmg, zip, or pkg files, you can specify a bundle, like an application (.app).
When a bundle is provided, this script will automatically package it in a temporary zip archive before uploading.
When notarization is completed, the bundle will be stapled and the temporary zip will be deleted.
You can specify an output directory if you want your notarized package or bundle to be copied elsewhere.
If you don't specify an output directory, the package or bundle will be stapled in place.
You can disable this script's notarization process by specifying environment variable named PACE_EDEN_NOTARIZE_DISABLE with a value of YES, TRUE, ON, or 1 (case insensitive).
When notarization is disabled, this script will still copy incoming packages or bundles to the output directory, if specified.
This way you can globally enable or disable actual notarization as appropriate without making changes to the rest of your build system.
"""

#-----------------------------------------------------------------------------------------
def main(argv):
	global gDebug
	global gQuiet
	global gSleepSeconds
	global gLogFile
	global gSeparator
	global gStapleUnsupportedExtensions
	global gDisableNotarizationEnvVarName

	usage="""build.py [OPTIONS]"""

	# Initialize the options parser for this script
	parser = OptionParser(usage=usage, description=gDescription)
	parser.set_defaults(examples=False, package=None, username=None, password=None, team_id=None, keychain_profile=None, primary_bundle_id=None, asc_provider=None, outdir=None, log=None, quiet=False, debug=False)
	parser.add_option("-x", "--examples",
		action="store_true", dest="examples",
		help="Show examples of how to use this script.")
	parser.add_option("-f", "--file",
		action="store", dest="package",
		help="Path to the package file to notarized. Generally this would be a dmg, zip, or pkg. You can also specify a bundle (like a .app). When you provide a bundle, this script will zip it up in a temp folder, perform the notarization, then staple the bundle. When used in combination with the --outdir option, the bundle will be copied to the specified output directory first, then stapled for you.")
	parser.add_option("-u", "--username",
		action="store", dest="username",
		help="Apple ID username to use to notarize.")
	parser.add_option("-p", "--password",
		action="store", dest="password",
		help="The app-specific password for the user account. Rather than specify the password directly, you can use @env:YOUR_ENV_VAR or @keychain:YOUR_KEYCHAIN_PW_NAME")
	parser.add_option("-t", "--team-id",
		action="store", dest="team_id",
		help="The team ID to use. If specified, then this script will use Apple's notarytool instead of the deprecated altool. Must be used in combination with the --username option. If you don't provide the --password option, the tool will ask you for your password.")
	parser.add_option("-k", "--keychain-profile",
		action="store", dest="keychain_profile",
		help="The keychain profile to use, previously created using the notarytool store-credentials command. If specified, then this script will use Apple's notarytool instead of the deprecated altool.")
	parser.add_option("--primary-bundle-id",
		action="store", dest="primary_bundle_id",
		help="Bundle ID of package to notarize. If you provide a bundle instead of a package file, then this option is not needed because this script will use bundle's ID by default.")
	parser.add_option("--asc-provider",
		action="store", dest="asc_provider",
		help="Required when a user account is associated with multiple providers.")
	parser.add_option("-o", "--outdir",
		action="store", dest="outdir",
		help="Optional output directory. If specified, then the package file will be copied here and stapled after successfully notarizing.")
	parser.add_option("-l", "--log",
		action="store", dest="log",
		help="Optional log file. If specified, then all script output will be written to this file.")
	parser.add_option("-q", "--quiet",
		action="store_true", dest="quiet",
		help="If specified, then don't provide any auditory feedback regarding the success or failure.")
	parser.add_option("-d", "--debug",
		action="store_true", dest="debug",
		help="Turn on debugging output for this script.")

	# Parse the incomming arguments.
	(options, args) = parser.parse_args()

	# See if we're debugging this script
	#options.debug=True
	if options.debug:
		gDebug = True
	else:
		gDebug = False

	if gDebug:
		p("After options parsing:")
		p("	 options:", options)
		p("	 args...:", args)

	if options.quiet:
		gQuiet = True
	else:
		gQuiet = False
	
	if options.examples:
		showExamples()
		sys.exit(0)

	# If the caller specified a log file, then open it now
	if options.log:
		logParentDir = os.path.realpath(os.path.dirname(options.log))
		mkdir_p(logParentDir)
		gLogFile = open(options.log, 'w')
		
	try:
		# Verify arguments
		package = options.package
		validatePathOption(package, "file", mustExist=True)

		# Remove any trailing backslash from the package we're notarizing
		package = package.rstrip('/')
		
		# See if the caller wants to use notary tool or not
		useNotarytool = False
		if options.team_id or options.keychain_profile:
			# Verify that notarytool is available on this system
			notarytoolPath = getNotarytoolPath()
			if len(notarytoolPath) == 0:
				e1("The options you've specified indicate that you want to use notarytool, but that tool is not installed on this system.")
				exitFail()
			useNotarytool = True
		
		# If the caller wants to use notary tool, then they either need to provide
		# a keychain profile, or a user, password, and team ID.
		validateUserAndPassword = False
		if useNotarytool:
			if options.keychain_profile:
				if not stringArgCheck(options.keychain_profile):
					e1("You have to specify a valid --keychain-profile.")
					exitFail()
					
				if options.team_id:
					e1("You cannot specify both a --team-id and a --keychain-profile.")
					exitFail()
			else:
				if not stringArgCheck(options.team_id):
					e1("You have to specify a valid --team-id.")
					exitFail()
				validateUserAndPassword = True
			
			# Apple's notarytool does not accept a primary bundle ID
			if options.primary_bundle_id:
				e1("The options you've specified indicate that you want to use notarytool. You cannot use the --primary-bundle-id option with notarytool.")
				exitFail()
				
		# Check the user name and password options
		if validateUserAndPassword or not useNotarytool:
			if not stringArgCheck(options.username):
				e1("You have to specify a valid --username.")
				exitFail()
			if not stringArgCheck(options.password):
				if useNotarytool:
					p("Warning! You're using notarytool but have not specified a notarization application password. You will be prompted to enter a password at upload time.")
				else:
					e1("You have to specify a valid --password.")
					exitFail()

		# We can't verify the requirement of a specified bundle ID until after we determine
		# if we're dealing with a package file or a bundle. Once that's known we can appropriately
		# verify the primary_bundle_id argument as needed.
		bundleIdToUse = options.primary_bundle_id
		
		# Determine if the caller wants to disable actual notarization
		doNotarize = True
		disableNotarizationEnvVarValue = getEnvVarValue(gDisableNotarizationEnvVarName).lower()
		if disableNotarizationEnvVarValue in gBooleanTrueStrings:
			doNotarize = False
		
		# Remember the start time so that we can indicate an elapsed time at the end
		startTotal = time.time()
	
		# If notarization is not suppressed, then do it now
		if doNotarize:
			# Establish a try handler to delete the temp dir, if created
			tempDir = None
			try:
				# If the caller provided a directory as the package, then  we'll need to zip it
				# into a temp archive.
				zipArchive = None
				if os.path.isdir(package):
					# If the caller is not using notarytool and we're dealing with a bundle
					# (not a framework), then we need to obtain its bundle ID.
					if not useNotarytool:
						packageExt = os.path.splitext(package)[1]
						if packageExt != ".framework":
							packagePlistFile = os.path.join(package, "Contents", "Info.plist")
							if isExistingRealFile(packagePlistFile):
								infoPlist = readPlistFromFile(packagePlistFile)
								if "CFBundleIdentifier" in infoPlist:
									packageBundleId = infoPlist["CFBundleIdentifier"]
									if bundleIdToUse and (bundleIdToUse	 != packageBundleId):
										p(gSeparatorMinor)
										p("IMPORTANT!")
										p("")
										p("You specified this bundle identifier as a command line option:       " + bundleIdToUse)
										p("But the target package you're trying to notarize has this bundle ID: " + packageBundleId)
										p("")
										p("We will use your specified bundle ID for this notarization request, but you should consider")
										p("calling this script again without passing the --primary-bundle-id option so that the bundle")
										p("ID of the target will be used instead.")
										p(gSeparatorMinor)
									else:
										bundleIdToUse = packageBundleId
										p("Using this discovered bundle ID as the primary bundle ID for notatization: " + bundleIdToUse)
								else:
									e1("You specified a bundle that has no bundle identifier.")
									exitFail()
							else:
								e1("You specified a package directory that does not appear to be a bundle.")
								exitFail()
					
					# Make a temp directory and zip the bundle or framework to it
					tempDir = tempfile.mkdtemp()		
					zipArchive = os.path.join(tempDir, os.path.basename(package))
					zipArchive = zipArchive + ".zip"
					p("Zipping up the target bundle to this temp location: " + zipArchive)
					cmd = 'ditto -ck --rsrc --sequesterRsrc --keepParent "' + package + '" "' + zipArchive + '"'
					(result, outputList) = runCommandAndExitOnError(cmd, "Error zipping up a temp copy of the bundle to notarize.", printOutput=False)
					package = zipArchive
	
				# At this stage either the caller provided a bundle ID to use, or we extracted it
				# from the target bundle. If neither happened and the caller is not using notarytool,
				# then it's a fatal error.
				if not useNotarytool and not stringArgCheck(bundleIdToUse):
					e1("You have to specify a valid --primary-bundle-id.")
					exitFail()
	
				# Upload the package to Apple
				p(gSeparator)
				(uuid, alreadyUploaded) = uploadPackage(package, options.username, options.password, options.asc_provider, bundleIdToUse, useNotarytool, options.team_id, options.keychain_profile)
	
				# If the package wasn't already uploaded, then wait until the notarization succeeds or fails. 
				if not useNotarytool and not alreadyUploaded:
					p(gSeparator)
					waitTillNotarizationDone(uuid, options.username, options.password)
			
				# If we get here then the package was successfully notarized. 
				p("Notarization of the posted package succeeded.")
		
				# If we zipped up the target app, then we need to staple the original package,
				# or the destination package if the caller specified an output directory.
				if zipArchive:
					package = options.package
	
				# If the caller wants us to copy the package before stapling, then do that now
				p(gSeparator)
				if options.outdir:
					# Do the copy
					destPackage = copyPackageToOutputDirectory(options.outdir, options.package)

					# We need to staple the copied package, not the original one. So change
					# the package path to the destination and fall through for stapling.
					package = destPackage
	
				# Staple the package, if it has a supported extension
				packageExt = os.path.splitext(package)[1]
				if packageExt in gStapleUnsupportedExtensions:
					p("Not stapling this package because it's unsupported: " + package)
				else:
					staplePackage(package)
			finally:
				# Remove our temp directory
				if tempDir:
					shutil.rmtree(tempDir)
		else:
			p("Notarization is suppressed by the " + gDisableNotarizationEnvVarName + " env var.")
			if options.outdir:
				destPackage = copyPackageToOutputDirectory(options.outdir, options.package)
		
		# Compute the elapsed time for all wrap operations
		elapsedTotal = time.time() - startTotal

		# Output the total time
		p(gSeparator)
		p("Total time:     " + humanReadableSeconds(elapsedTotal, inIncludeSeconds=True))

		exitSuccess()
	finally:
		if gLogFile:
			gLogFile.close()
	
#-----------------------------------------------------------------------------------------
if __name__ == "__main__":
	main(sys.argv)
