#!/bin/bash
# Variables Iniciales
# Script: test

ftpuser="usuario"
ftppassword='a"contrasenia'
recordingremotehost="usftpcorp.dominio.com"
remotedirPath="/speechanalytics"
remotedir=""

logFile="/tmp/LogUploadFilesToFTPTest.log"

inFile="/tmp/example.txt"

remotedir=$remotedirPath"/"$(date -r $inFile '+%Y' | bc)"/"$(date -r $inFile '+%m' | bc)"/"$(date -r $inFile '+%d' | bc)

cmd='lftp -u $ftpuser,$ftppassword sftp://$recordingremotehost -e "mkdir -p $remotedir;put -O $remotedir $inFile; bye"'

eval $cmd >> $logFile 2>&1 # En el FTP hay una carpeta /GrabacionesWAV/2021/9/27
uploaded=$?

echo $uploaded