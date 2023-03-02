#!/bin/bash
# Variables Iniciales
# Script: UploadFailedToFTP
# Description: It runs every 1 minutes, and transfers files older than 1 minutes to MW Server through TFTP
# Args:
# $1: Number Of Queue  


# Variables Iniciales
numberOfAudios=100
timeForFile=1 # Tiempo de antiguedad del file para que se procese
directory="/GrabacionesWAV"
logFile="/tmp/LogUploadFailedToFTP.log"
LogEnabled=1
echo $logFile
PATHFILES="/GrabacionesWAV/q$1"
FAILEDPATH="/GrabacionesWAVFailed/q$1"
inFile=""
outFile=""
ftpuser="administrator"
ftppassword="PassWordFTP123"
recordingremotehost="10.150.71.3"
remotedir="/Speech\ Analytics"



logInfo() {
	if [ "$LogEnabled" == 1 ]; then
		echo `date +%r-%N`  "INFO: $1" >> $logFile
	fi
}

logError() {
		echo `date +%r-%N` "ERROR: $1" >> $logFile
}

main(){	

	logInfo "ps aux | grep '/usr/sbin/UploadFailedToFTP.sh $1' | wc -l"
	
	if [ "$1" == ""  ]; then
		logInfo "Debe especificar un argumento"
		exit 1
	fi

	inProcess=$(ps aux | grep '/usr/sbin/UploadFailedToFTP.sh $1' | wc -l)
	echo inProces = $inProcess
	if [ "$inProcess" -ge "3" ]; then
		logInfo "Otro proceso esta en memoria"
		exit 1
	fi 
	
	logInfo "Procesando archivos"	
	logInfo "Procensado como máximo $numberOfAudios "

	for file in $(find $FAILEDPATH -maxdepth 1 -type f -print | head -n$numberOfAudios)
	do
		
		local uploaded=0;
		inFile=$file
		callIdentification=$(basename $inFile)
		callIdentification=$(sed 's/.wav//g' <<< $callIdentification)
		if [[ -f $inFile ]]; then
			logInfo "$inFile exist. Uploading files to MW Server"
			logInfo "Executing FTP command"
						
			remotedir=$remotedir"/"$(date -r $inFile '+%Y' | bc)"/"$(date -r $inFile '+%m' | bc)"/"$(date -r $inFile '+%d' | bc)
	
			cmd="ncftpput -V -t 10 -u $ftpuser -p $ftppassword -P 2121 -m $recordingremotehost"
			eval $cmd "$remotedir" $inFile >> $logFile 2>&1 # En el FTP hay una carpeta /GrabacionesWAV/2021/09/27
			uploaded=$?
			if [ "$uploaded" == 0 ]; then
				logInfo "Files uploded ok to ftp server for id:$callIdentification" 
				rm -f $inFile  

			else
				logError "Error transfering file for $callIdentification to ftp server" 
				#mv $inFile "$FAILEDPATH/."
				logInfo "Fallo $callIdentification. Procesando siguiente archivo."	
			fi
		fi
	done
}

main $1
