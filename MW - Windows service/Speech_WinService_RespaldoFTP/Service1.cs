using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Threading;
using System.Timers;
using System.ServiceProcess;
using System.Linq;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using System.Configuration;
using System.Net.Mail;
using System.Net;
using System.Web.Script.Serialization;
using System.Runtime.InteropServices;

namespace inConcertSpeechRespaldoSFTP
{
    public partial class Service1 : ServiceBase
    {
        public const string APP_NAME = "inConcertSpeechRespaldoSFTP";
        private static System.Timers.Timer timer;

        private static string LOG_PATH = "C:\\Windows\\SysWOW64\\config\\systemprofile\\inConcert\\Logs\\";

        public Service1()
        {
            InitializeComponent();
            ServiceName = "inConcertSpeechRespaldoSFTP";
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                timer = new System.Timers.Timer();
                timer.Elapsed += new ElapsedEventHandler(OnTimedEventGenerarReporte);
                timer.Interval = 10000;
                timer.Enabled = true;
            }
            catch (Exception ex)
            {
                logError("Error al iniciar - " + ex.Message + " - " + ex.StackTrace);
            }
        }

        protected override void OnStop()
        {
            timer.Enabled = false;
        }

        private void OnTimedEventGenerarReporte(object source, ElapsedEventArgs e)
        {

            //--logDebug("OnTimedEventGenerarReporte");

            timer.Stop();
            GC.Collect();

            try
            {
                DateTime tmstmp = DateTime.UtcNow;
                procesar(tmstmp);
            }
            catch (Exception ex)
            {
                logError("Error al procesar - " + ex.Message + " - " + ex.StackTrace);
            }
            int tiempo_minutos = Int32.Parse(ConfigurationManager.AppSettings["tiempo_minutos"]);
            timer.Interval = tiempo_minutos * 60 * 1000;
            timer.Start();

        }

        private void procesar(DateTime tmstmp)
        {

            try
            {
                string temp_folder = ConfigurationManager.AppSettings["Temp_Folder"];
                string[] folders = System.IO.Directory.GetDirectories(temp_folder, "*", System.IO.SearchOption.AllDirectories);

                ProcesarArchivosYFolders(folders,
                            ConfigurationManager.AppSettings["SFTP"],
                            ConfigurationManager.AppSettings["SFTP_port"],
                            ConfigurationManager.AppSettings["SFTP_user"],
                            ConfigurationManager.AppSettings["SFTP_password"]);
               


            }
            catch (Exception ex)
            {
                logError("Error al Procesar - " + ex.Message + " - " + ex.StackTrace);
            }
            
        }

        public void ProcesarArchivosYFolders(string[] sftp_folders_create, string url_sftp, string sftp_port, string usuario, string pass)
        {
            string temp_directory = ConfigurationManager.AppSettings["Temp_Directory"];

            try
            {
               
                if(sftp_folders_create.Length > 0) { 
                    using (var client = new SftpClient(url_sftp, Int32.Parse(sftp_port), usuario, pass))
                    {
                        client.Connect();

                        // GENERO CARPETAS EN CASO FALTE

                        foreach (string folder in sftp_folders_create)
                        {
                            try
                            {
                                    string CarpetaSFTP = folder.Replace(temp_directory, "").Replace("\\", "/");
                                //string rutaFTP_t =  CarpetaSFTP;

                                logDebug("SubirCarpetaFTP - " + folder);


                                if (!client.Exists(CarpetaSFTP))
                                {
                                    client.CreateDirectory(CarpetaSFTP);
                                }
                                else {
                                    logDebug("SubirCarpetaFTP - La carpeta ya existe: " + folder);
                                }
                            }
                            catch (WebException ex3)
                            {
                                logError("SubirCarpetaFTP - Error: " + folder  + ", " + ex3.Message + " - " + ex3.StackTrace);
                            }

                        }


                        foreach (string folder in sftp_folders_create)
                        {
                            bool delete_folder = false;

                            try
                            {
                                logDebug("SubirArchivosDeFolderFTP - " + folder + ", " + folder.Replace("\\", "/").Replace(temp_directory, ""));

                                string[] Archivos = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories);
                                string[] subfolders = System.IO.Directory.GetDirectories(folder, "*", System.IO.SearchOption.AllDirectories);

                                logDebug("Folder: " + folder + ", Archivos: " + Archivos.Length + ", Subfolders: " + subfolders.Length);

                            
                                string[] sub_folders = folder.Replace("\\", "/").Replace(temp_directory, "").Split('/');

                                if (Archivos.Length == 0 && subfolders.Length == 0)
                                {
                                    delete_folder = true; // Quiero eliminar las que tengan vacios

                                    if (sub_folders.Length == 5) // Valido para las rutas completas (anio/mes/dia) si la fecha es antes de hoy, de ser asi y no tiene archivos elimino la carpeta
                                    {
                                        DateTime fecha_carpeta = new DateTime(Int32.Parse(sub_folders[2]), Int32.Parse(sub_folders[3]), Int32.Parse(sub_folders[4]));
                                        DateTime fecha_actual = DateTime.UtcNow;
                                        if (DateTime.Compare(fecha_carpeta, fecha_actual.AddDays(-1)) < 0)
                                        {
                                            delete_folder = true;
                                        }
                                        else
                                        {
                                            delete_folder = false;
                                        }
                                    }

                                    if (delete_folder)
                                    {
                                        logDebug("DeleteLocalFolder - Folder: " + delete_folder);
                                        System.IO.Directory.Delete(folder);
                                    }
                                }


                                if (Archivos.Length > 0 && subfolders.Length == 0)
                                {

                                    foreach (string Archivo in Archivos)
                                    {

                                        string ArchivoFinal = Archivo.Replace("\\", "/").Replace(temp_directory, "");

                                        logDebug("Subo a SFTP: " + ArchivoFinal);

                                        int limite_t = 255;

                                        if (Archivo.Length <= limite_t)
                                        {
                                            bool procesado = false;

                                            client.ChangeDirectory(ArchivoFinal.Replace("/" + Path.GetFileName(Archivo), ""));

                                            try
                                            {
                                                using (var fileStream = new FileStream(Archivo, FileMode.Open))
                                                {
                                                    client.BufferSize = 4 * 1024; // bypass Payload error large files
                                                    client.UploadFile(fileStream, Path.GetFileName(Archivo));
                                                    string fileSize = Convert.ToString(fileStream.Length);
                                                };

                                                logDebug("SubirArchivoFTP - " + ArchivoFinal);
                                                procesado = true;
                                            }
                                            catch (WebException ex4)
                                            {
                                                logError("SubirArchivoFTP - " + ArchivoFinal + ", " + ex4.Message + " - " + ex4.StackTrace);
                                                procesado = false;
                                            }



                                            if (procesado)
                                            {


                                                try
                                                {
                                                    logDebug("Intento eliminar el archivo: " + Archivo);
                                                    System.IO.File.Delete(Archivo);
                                                }
                                                catch (Exception ex5)
                                                {
                                                    logError("Intento eliminar el archivo: " + Archivo + ", " + ex5.Message + " - " + ex5.StackTrace);
                                                }
                                            }

                                        }
                                        else
                                        {
                                            logDebug("El archivo tiene mas de 255 caracteres: " + Archivo);

                                            try
                                            {
                                                logDebug("Intento eliminar el archivo: " + Archivo);
                                                System.IO.File.Delete(Archivo);
                                            }
                                            catch (Exception ex5)
                                            {
                                                logError("Intento eliminar el archivo: " + Archivo + ", " + ex5.Message + " - " + ex5.StackTrace);
                                            }
                                        }

                                    }
                                }
                            }
                            catch (Exception ex2)
                            {
                                logError("Error al procesar archivos de: " + folder + ", " + ex2.Message + " - " + ex2.StackTrace);
                            }
                        }



                        client.Disconnect();
                    }
                }

            }
            catch (Exception ex1)
            {
                logError("Error en ProcesarArchivosYFolders: " + ex1.Message + " - " + ex1.StackTrace);
            }
        }

        public static string ConnectToShare(string uri, string username, string password)
        {
            try
            {
                //Create netresource and point it at the share
                NETRESOURCE nr = new NETRESOURCE();
                nr.dwType = RESOURCETYPE_DISK;
                nr.lpRemoteName = uri;

                //Create the share
                int ret = WNetUseConnection(IntPtr.Zero, nr, password, username, 0, null, null, null);

                //Check for errors
                if (ret == NO_ERROR)
                    return null;
                else
                    return GetError(ret);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            
        }

        public static string DisconnectFromShare(string uri, bool force)
        {
            try
            {
                //remove the share
                int ret = WNetCancelConnection(uri, force);

                //Check for errors
                if (ret == NO_ERROR)
                    return null;
                else
                    return GetError(ret);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            
        }

        private void logError(String text)
        {
            try
            {
                LoggingExtensions.WriteDebug(LOG_PATH, APP_NAME, "[ERROR]	" + DateTime.Now.ToString("yyyyMMdd_HH:mm:ss") + "	" + text);

                if (!System.Diagnostics.EventLog.SourceExists(APP_NAME))
                    System.Diagnostics.EventLog.CreateEventSource(APP_NAME, "Application");

                System.Diagnostics.EventLog.WriteEntry(APP_NAME, text, System.Diagnostics.EventLogEntryType.Error);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        private void logDebug(String text)
        {
            try
            {
                LoggingExtensions.WriteDebug(LOG_PATH, APP_NAME, "[DEBUG]	" + DateTime.Now.ToString("yyyyMMdd_HH:mm:ss") + "	" + text);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }
        
        #region P/Invoke Stuff
        [DllImport("Mpr.dll")]
        private static extern int WNetUseConnection(
            IntPtr hwndOwner,
            NETRESOURCE lpNetResource,
            string lpPassword,
            string lpUserID,
            int dwFlags,
            string lpAccessName,
            string lpBufferSize,
            string lpResult
            );

        [DllImport("Mpr.dll")]
        private static extern int WNetCancelConnection(
            string lpName,
            bool fForce
            );

        [StructLayout(LayoutKind.Sequential)]
        private class NETRESOURCE
        {
            public int dwScope = 0;
            public int dwType = 0;
            public int dwDisplayType = 0;
            public int dwUsage = 0;
            public string lpLocalName = "";
            public string lpRemoteName = "";
            public string lpComment = "";
            public string lpProvider = "";
        }

        #region Consts
        const int RESOURCETYPE_DISK = 0x00000001;
        const int CONNECT_UPDATE_PROFILE = 0x00000001;
        #endregion

        #region Errors
        const int NO_ERROR = 0;

        const int ERROR_ACCESS_DENIED = 5;
        const int ERROR_ALREADY_ASSIGNED = 85;
        const int ERROR_BAD_DEVICE = 1200;
        const int ERROR_BAD_NET_NAME = 67;
        const int ERROR_BAD_PROVIDER = 1204;
        const int ERROR_CANCELLED = 1223;
        const int ERROR_EXTENDED_ERROR = 1208;
        const int ERROR_INVALID_ADDRESS = 487;
        const int ERROR_INVALID_PARAMETER = 87;
        const int ERROR_INVALID_PASSWORD = 1216;
        const int ERROR_MORE_DATA = 234;
        const int ERROR_NO_MORE_ITEMS = 259;
        const int ERROR_NO_NET_OR_BAD_PATH = 1203;
        const int ERROR_NO_NETWORK = 1222;
        const int ERROR_SESSION_CREDENTIAL_CONFLICT = 1219;

        const int ERROR_BAD_PROFILE = 1206;
        const int ERROR_CANNOT_OPEN_PROFILE = 1205;
        const int ERROR_DEVICE_IN_USE = 2404;
        const int ERROR_NOT_CONNECTED = 2250;
        const int ERROR_OPEN_FILES = 2401;

        private struct ErrorClass
        {
            public int num;
            public string message;
            public ErrorClass(int num, string message)
            {
                this.num = num;
                this.message = message;
            }
        }

        private static ErrorClass[] ERROR_LIST = new ErrorClass[] {
        new ErrorClass(ERROR_ACCESS_DENIED, "Error: Access Denied"),
        new ErrorClass(ERROR_ALREADY_ASSIGNED, "Error: Already Assigned"),
        new ErrorClass(ERROR_BAD_DEVICE, "Error: Bad Device"),
        new ErrorClass(ERROR_BAD_NET_NAME, "Error: Bad Net Name"),
        new ErrorClass(ERROR_BAD_PROVIDER, "Error: Bad Provider"),
        new ErrorClass(ERROR_CANCELLED, "Error: Cancelled"),
        new ErrorClass(ERROR_EXTENDED_ERROR, "Error: Extended Error"),
        new ErrorClass(ERROR_INVALID_ADDRESS, "Error: Invalid Address"),
        new ErrorClass(ERROR_INVALID_PARAMETER, "Error: Invalid Parameter"),
        new ErrorClass(ERROR_INVALID_PASSWORD, "Error: Invalid Password"),
        new ErrorClass(ERROR_MORE_DATA, "Error: More Data"),
        new ErrorClass(ERROR_NO_MORE_ITEMS, "Error: No More Items"),
        new ErrorClass(ERROR_NO_NET_OR_BAD_PATH, "Error: No Net Or Bad Path"),
        new ErrorClass(ERROR_NO_NETWORK, "Error: No Network"),
        new ErrorClass(ERROR_BAD_PROFILE, "Error: Bad Profile"),
        new ErrorClass(ERROR_CANNOT_OPEN_PROFILE, "Error: Cannot Open Profile"),
        new ErrorClass(ERROR_DEVICE_IN_USE, "Error: Device In Use"),
        new ErrorClass(ERROR_EXTENDED_ERROR, "Error: Extended Error"),
        new ErrorClass(ERROR_NOT_CONNECTED, "Error: Not Connected"),
        new ErrorClass(ERROR_OPEN_FILES, "Error: Open Files"),
        new ErrorClass(ERROR_SESSION_CREDENTIAL_CONFLICT, "Error: Credential Conflict"),
    };

        private static string GetError(int errNum)
        {
            foreach (ErrorClass er in ERROR_LIST)
            {
                if (er.num == errNum) return er.message;
            }
            return "Error: Unknown, " + errNum;
        }
        #endregion

        #endregion

    }
}
