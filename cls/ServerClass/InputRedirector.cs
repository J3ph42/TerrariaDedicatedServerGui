using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.ComponentModel;
using System.Threading;

namespace Local.Server
{
    class InputRedirector : IDisposable
    {

        #region Declaration
        //Declaration

        private Boolean boolDisposed = false;
        private Boolean boolExitForced = false;

        private Boolean boolLockVersion = false;
        private String strVersion = String.Empty;

        private Boolean boolAllowAdmin = false;
        private String strAdmin = String.Empty;

        private Boolean boolLockTime = false;
        private Boolean boolLockMotd = false;
        private Boolean boolLockPlaying = false;

        private Boolean boolAllowUserTime = false;

        private Byte byteForceTime = 0;

        private System.Timers.Timer timerForceTime = new System.Timers.Timer(); //Night 9m Realtime = 9h Gametime / Day 15m Realtime = 15 Gametime

        private Int64 intCount = 0;

        private Process procInputController;
        private Int32 intPriority = 1;

        private String strWorkingPath = String.Empty;
        private String strFileName = String.Empty;
        private StringBuilder strbldArguments = new StringBuilder();

        private String strBufferOut = String.Empty;
        private String strBufferOutLower = String.Empty;

        private Int32 intPlayer = 0;

        private Boolean boolRunning = false;

        private BackgroundWorker bkwrkrHelper = new BackgroundWorker();

        #endregion

        #region Properties
        //Properties

        public String WorkingPath
        {//FileName
            set { this.strWorkingPath = value; }
        }

        public String FileName
        {//FileName
            set { this.strFileName = this.strWorkingPath + value; }
        }

        public String Arguments
        {//Arguments
            set { this.strbldArguments.AppendLine(value); }
        }

        public String Command
        {//Command
            set
            {
                if (this.procInputController != null && this.boolRunning)
                {
                    this.procInputController.StandardInput.WriteLine(value);
                }
            }
        }

        public Boolean AllowAdmin
        {//Allow Admin (required for Admin Chat Message)
            set { this.boolAllowAdmin = value; }
        }

        public String Admin
        {//Admin Name (required for Admin Chat Message)
            set { this.strAdmin = value; }
        }

        public Boolean AllowUserTime
        {//Allow User to change Time
            set { this.boolAllowUserTime = value; }
        }

        public Int32 Player
        {//Player
            get { return this.intPlayer; }
        }

        public String Buffer
        {//Buffer
            get { return this.strBufferOut; }
        }

        public Boolean IsBusy
        {//IsBusy
            get { return this.bkwrkrHelper.IsBusy; }
        }

        public Int32 Priority
        {
            set { this.intPriority = value; }
        }

        public Boolean Running
        {//Running
            get { return this.boolRunning; }
        }

        #endregion

        #region Add Events
        //Add Events

        public delegate void EventHandler();

        //Helper
        public event EventHandler ProgressChanged;
        public event EventHandler Completed;

        #endregion

        #region Event Handler
        //Event Handler

        private void SetProgressChanged()
        {
            if (ProgressChanged != null && !this.boolExitForced)
            {
                ProgressChanged();
            }
        }

        private void SetCompleted()
        {
            if (Completed != null && !this.boolExitForced)
            {
                Completed();
            }
        }

        #endregion

        #region Events BackgroundWorker
        //Events BackgroundWorker

        private void bwHelper_DoWork(object sender, DoWorkEventArgs e)
        {
            this.DoJob();
        }

        private void bwHelper_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            this.SetCompleted(); //report ui new data avaible
        }

        #endregion

        #region Construct
        //Construct

        public InputRedirector()
        {
            this.bkwrkrHelper.WorkerReportsProgress = true;

            this.bkwrkrHelper.DoWork += new DoWorkEventHandler(bwHelper_DoWork);
            this.bkwrkrHelper.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bwHelper_RunWorkerCompleted);
        }

        #endregion

        #region Init
        //Init

        public void Init()
        {
            this.timerForceTime.Interval = 4 * 60 * 1000;
            this.timerForceTime.AutoReset = true;
            this.timerForceTime.Enabled = true;
            this.timerForceTime.Elapsed += new System.Timers.ElapsedEventHandler(tForceTime_Elapsed);
        }

        #endregion

        #region DoJobAsync
        //DoJobAsync

        public void DoJobAsync()
        {
            if (!this.bkwrkrHelper.IsBusy)
            {
                this.bkwrkrHelper.RunWorkerAsync();
            }
        }

        #endregion

        #region DoJob
        //DoJob

        private void DoJob()
        {
            this.ProcessController();
        }

        #endregion

        #region Process - Controller
        //Process - InputRedirector

        private void ProcessController()
        {
            using (this.procInputController = new Process())
            {
                this.procInputController.StartInfo.UseShellExecute = false;
                this.procInputController.StartInfo.RedirectStandardError = true;
                this.procInputController.StartInfo.RedirectStandardOutput = true;
                this.procInputController.StartInfo.RedirectStandardInput = true;
                this.procInputController.StartInfo.CreateNoWindow = true;
                this.procInputController.EnableRaisingEvents = true;
                


                ProcessPriorityClass ppcController;

                switch (this.intPriority)
                {
                    case 0:
                        ppcController = ProcessPriorityClass.RealTime;
                        break;
                    case 1:
                        ppcController = ProcessPriorityClass.High;
                        break;
                    case 2:
                        ppcController = ProcessPriorityClass.AboveNormal;
                        break;
                    case 3:
                        ppcController = ProcessPriorityClass.Normal;
                        break;
                    case 4:
                        ppcController = ProcessPriorityClass.BelowNormal;
                        break;
                    case 5:
                        ppcController = ProcessPriorityClass.Idle;
                        break;
                    default:
                        ppcController = ProcessPriorityClass.Normal;
                        break;
                }

                this.procInputController.StartInfo.FileName = this.strFileName;
                this.procInputController.StartInfo.Arguments = this.strbldArguments.ToString().Replace("\r", "").Replace("\n", " ");
                this.procInputController.StartInfo.WorkingDirectory = this.strWorkingPath;

                this.procInputController.OutputDataReceived += pController_OutputDataReceived;
                this.procInputController.ErrorDataReceived += pController_ErrorDataReceived;

                this.procInputController.Exited += new System.EventHandler(pController_Exited);

                this.procInputController.Start();

                this.procInputController.PriorityClass = ppcController;

                this.boolRunning = true;

                this.procInputController.BeginErrorReadLine();
                this.procInputController.BeginOutputReadLine();

                this.procInputController.WaitForExit();
            }
        }

        #endregion

        #region Process - Error Receiver
        //Process - Error Receiver

        private void pController_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                this.strBufferOut = (String.Format("ERROR: {0}", e.Data));
                this.SetProgressChanged();
            }
        }

        #endregion

        #region Process - Data Receiver
        //Process - Data Receiver

        private void pController_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            this.intCount += 1; //raise received Messages Count +1

            if (e.Data != null)
            {
                this.strBufferOut = e.Data;
                this.strBufferOutLower = e.Data.ToLower();

                if (this.intCount <= 30 && !this.boolLockVersion) //workarround get Terraria Server vx.x.x
                {
                    if (this.strBufferOutLower.Contains("terraria server v"))
                    {
                        this.strVersion = strBufferOut;
                        this.boolLockVersion = true;
                    }
                }
                else
                {
                    this.strBufferOut = this.strBufferOut.Replace(this.strVersion, "");
                }

                if (this.strBufferOutLower.Contains("has joined"))
                {
                    intPlayer += 1;
                }

                if (this.strBufferOutLower.Contains("has left"))
                {
                    intPlayer -= 1;
                }

                //dirty! User could type Chat Message: blah <ADMINNAME> SERVER COMMAND
                //ToDo: fix it
                if (this.boolAllowAdmin && this.strBufferOut.Contains("<" + this.strAdmin + "> /"))
                {
                    String sBufferLocal = Regex.Replace(this.strBufferOut, "\\s+", "");

                    sBufferLocal = Regex.Replace(sBufferLocal, "<" + this.strAdmin + ">", "");
                    sBufferLocal = Regex.Replace(sBufferLocal, ":", "");
                    sBufferLocal = Regex.Replace(sBufferLocal, "/", "");

                    this.procInputController.StandardInput.WriteLine(sBufferLocal);
                }
                else
                {
                    if (this.boolLockTime && this.strBufferOutLower.Contains("time") && !this.strBufferOut.Contains("Server"))
                    {//Server send back Chat Message: time (Step 2)
                        this.boolLockTime = false;
                        this.procInputController.StandardInput.WriteLine("say Game " + this.strBufferOut + " - Server Time: " + DateTime.Now.ToShortTimeString());
                    }

                    if (this.strBufferOutLower.Contains("> time") && !this.strBufferOut.Contains("Server"))
                    {//User send Chat Message: time (Step 1)
                        this.boolLockTime = true;
                        this.procInputController.StandardInput.WriteLine("time");
                    }

                    if (this.boolLockMotd && this.strBufferOutLower.Contains("motd") && !this.strBufferOut.Contains("Server"))
                    {//Server send back Chat Message: motd (Step 2)
                        this.boolLockMotd = false;
                        this.procInputController.StandardInput.WriteLine("say " + this.strBufferOut);
                    }

                    if ((this.strBufferOutLower.Contains("> motd") || this.strBufferOutLower.Contains("help")) && !this.strBufferOut.Contains("Server") && !this.strBufferOutLower.Contains("type /"))
                    {//User send Chat Message: motd (Step 1)
                        this.boolLockMotd = true;
                        this.procInputController.StandardInput.WriteLine("motd");
                    }

                    if (this.boolLockPlaying && this.strBufferOutLower.Contains("(") && this.strBufferOutLower.Contains(")") && !this.strBufferOut.Contains("Server"))
                    {//Server send back Chat Message: playing (Step 2)
                        this.boolLockPlaying = false;
                        this.procInputController.StandardInput.WriteLine("say " + this.strBufferOut);
                    }

                    if (this.strBufferOutLower.Contains("> playing") && !this.strBufferOut.Contains("Server"))
                    {//User send Chat Message: playing (Step 1)
                        this.boolLockPlaying = true;
                        this.procInputController.StandardInput.WriteLine("playing");
                    }

                    if (this.boolAllowUserTime || (this.boolAllowAdmin && this.strBufferOut.Contains("<" + this.strAdmin + ">")))
                    {//dawn, noon, dusk or midnight
                        if (this.strBufferOutLower.Contains("> dawn"))
                        {
                            this.procInputController.StandardInput.WriteLine("dawn");
                            this.procInputController.StandardInput.WriteLine("say time set to dawn");
                        }

                        if (this.strBufferOutLower.Contains("> noon"))
                        {
                            this.procInputController.StandardInput.WriteLine("noon");
                            this.procInputController.StandardInput.WriteLine("say time set to noon");
                        }

                        if (this.strBufferOutLower.Contains("> dusk"))
                        {
                            this.procInputController.StandardInput.WriteLine("dusk");
                            this.procInputController.StandardInput.WriteLine("say time set to dusk");
                        }

                        if (this.strBufferOutLower.Contains("> midnight"))
                        {
                            this.procInputController.StandardInput.WriteLine("midnight");
                            this.procInputController.StandardInput.WriteLine("say time set to midnight");
                        }

                        if (this.strBufferOutLower.Contains("> forcedawn"))
                        {
                            this.procInputController.StandardInput.WriteLine("dawn");
                            this.byteForceTime = 1;
                            this.timerForceTime.Interval = 7 * 60 * 1000;
                            this.timerForceTime.Start();
                            this.procInputController.StandardInput.WriteLine("say time forced to dawn (use resetforce for reset)");
                        }

                        if (this.strBufferOutLower.Contains("> forcenoon"))
                        {
                            this.procInputController.StandardInput.WriteLine("noon");
                            this.byteForceTime = 2;
                            this.timerForceTime.Interval = 7 * 60 * 1000;
                            this.timerForceTime.Start();
                            this.procInputController.StandardInput.WriteLine("say time forced to noon (use resetforce for reset)");
                        }

                        if (this.strBufferOutLower.Contains("> forcedusk"))
                        {
                            this.procInputController.StandardInput.WriteLine("dusk");
                            this.byteForceTime = 3;
                            this.timerForceTime.Interval = 4 * 60 * 1000;
                            this.timerForceTime.Start();
                            this.procInputController.StandardInput.WriteLine("say time forced to dusk (use resetforce for reset)");
                        }

                        if (this.strBufferOutLower.Contains("> forcemidnight"))
                        {
                            this.procInputController.StandardInput.WriteLine("midnight");
                            this.byteForceTime = 4;
                            this.timerForceTime.Interval = 4 * 60 * 1000;
                            this.timerForceTime.Start();
                            this.procInputController.StandardInput.WriteLine("say time forced to midnight (use resetforce for reset)");
                        }

                        if (this.strBufferOutLower.Contains("> resetforce"))
                        {
                            this.byteForceTime = 0;
                            this.timerForceTime.Stop();
                            this.procInputController.StandardInput.WriteLine("say time force reseted");
                        }
                    }
                }

                this.SetProgressChanged();
            }
        }

        #endregion

        #region Process - Exited
        //Process - Exited

        private void pController_Exited(object sender, EventArgs e)
        {
            this.strbldArguments.Clear();
            this.intPlayer = 0;
            this.boolRunning = false;
        }

        #endregion

        #region Timer Elapsed
        //Timer Elapsed

        private void tForceTime_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            switch (this.byteForceTime)
            {
                case 1:
                    this.procInputController.StandardInput.WriteLine("dawn");
                    this.procInputController.StandardInput.WriteLine("say time forced to dawn (use resetforce for reset)");
                    break;
                case 2:
                    this.procInputController.StandardInput.WriteLine("noon");
                    this.procInputController.StandardInput.WriteLine("say time forced to noon (use resetforce for reset)");
                    break;
                case 3:
                    this.procInputController.StandardInput.WriteLine("dusk");
                    this.procInputController.StandardInput.WriteLine("say time forced to dusk (use resetforce for reset)");
                    break;
                case 4:
                    this.procInputController.StandardInput.WriteLine("midnight");
                    this.procInputController.StandardInput.WriteLine("say time forced to midnight (use resetforce for reset)");
                    break;
                default:
                    this.timerForceTime.Stop();
                    break;
            }
        }

        #endregion

        #region Process - Request - Exit
        //Process - Request - Exit

        public void RequestExit()
        {

            if (this.boolRunning)
            {
                this.Command = "1";

                Thread.Sleep(100);

                for (int i = 0; i < 5; i++)
                {
                    this.Command = "";
                    Thread.Sleep(100);
                }

                this.Command = "exit-nosave";
            }
        }

        #endregion

        #region implement idisposable
        // implement idisposable

        /// <summary>
        /// Release unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!boolDisposed)
            {
                if (disposing)
                {
                    // Free other state (managed objects).
                }
                // Release unmanaged resources.
                // Set large fields to null.
                // Call Dispose on your base class.

                this.procInputController.Dispose();

                boolDisposed = true;
            }
        }

        #endregion

    }
}
