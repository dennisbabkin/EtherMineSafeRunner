//  
//    EtherMineSafeRunner
//    "Watch utility for the ethminer - Ethereum GPU mining worker software."
//    Copyright (c) 2021 www.dennisbabkin.com
//    
//        https://dennisbabkin.com/blog/?i=AAA0D000
//    
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//    
//        https://www.apache.org/licenses/LICENSE-2.0
//    
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//  
//


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace EtherMineSafeRunner
{

	class Program
	{
		//Program defaults
		static public string gkstrAppVersion = "1.0.0";
		static public string gkstrAppName = "EtherMineSafeRunner";

		//Location of the mail server to dispatch notification emails:
		static public string gkstrNotificationHost = "https://example.com";							//Host name of the web server
		static public string gkstrNotificationPage = "/php/send_rig_notification_email.php";		//Page that dispatches a notification email
		static public string gkstrNotificationKey = "";												//Special private GUI to authenticate with the web server

		//PHP code for the web server to dispatch a notification email can be viewed at:
		//  https://dennisbabkin.com/blog/?i=AAA0D000#impl_send_email_php
		//


		public static WatchState gWS = new WatchState();           //Global parameters
		public static EventLog gEventLog = new EventLog();			//Event log for diagnotic logging


		static void Main(string[] args)
		{
			//Do we have command line arguments?
			if(args.Length > 0)
			{
				if (args.Length >= 4)
				{
					CmdLineParams clp = new CmdLineParams();
					int p = 0;

					if (uint.TryParse(args[p++], out clp.nHashRangeMin))
					{
						if (uint.TryParse(args[p++], out clp.nHashRangeMax))
						{
                            //          WHY?
							//if (clp.nHashRangeMax < clp.nHashRangeMin)
							//{
							//	//Swap them
							//	uint v = clp.nHashRangeMin;
							//	clp.nHashRangeMin = clp.nHashRangeMax;
							//	clp.nHashRangeMax = v;
							//}

							if (uint.TryParse(args[p++], out clp.nMaxAllowedMinerRestartsBeforeReboot))
							{
								//Then
								clp.strMinerExePath = args[p++];

								//Get command line parameters follow
								for (int a = p; a < args.Length; a++)
								{
									clp.arrMinerCmdParams.Add(args[a]);
								}


								//Set event log message
								gEventLog.logMessage(EventLogMsgType.ELM_TYP_Information, "[PID=" + Process.GetCurrentProcess().Id + "] Starting miner watch with parameters: " +
									String.Join(" ", args.ToArray()) +
									" -- Last boot time: " + GetLastBootTime().ToString("g"));

								//Set time when this app started
								gWS.setWhenThisAppStartedTimeUTC();

								//Begin a watching thread
								Thread threadWatch = new Thread(() => { threadWatchMiner(clp); });
								threadWatch.Start();

								//And run the miner app
								MinerRunningLoop(clp);



							}
							else
								OutputConsoleError("ERROR: Invalid RebootAfter");
						}
						else
							OutputConsoleError("ERROR: Invalid MaxHash");
					}
					else
						OutputConsoleError("ERROR: Invalid MinHash");
				}
				else
					OutputConsoleError("ERROR: Not all command line parameters were provided");

			}
			else
			{
				//Show command line args
				string strAppName = Process.GetCurrentProcess().ProcessName;

				Console.WriteLine(gkstrAppName + " v." + gkstrAppVersion);
				Console.WriteLine("by www.dennisbabkin.com");
				Console.WriteLine("");
				Console.WriteLine("Usage:");
				Console.WriteLine(strAppName + " MinHash MaxHash RebootAfter \"path-to\\ethminer.exe\" miner_commad_line");
				Console.WriteLine("");
				Console.WriteLine("where:");
				Console.WriteLine("  MinHash      = mininum allowed hash rate range (in Mh), ex: 80");
				Console.WriteLine("  MaxHash      = maximum allowed hash rate range (in Mh), or 0 if any, ex: 100");
				Console.WriteLine("  RebootAfter  = number of ethminer restarts before rebooting the rig, or 0 not to reboot");
				Console.WriteLine("");
				Console.WriteLine("Example:");
				Console.WriteLine("");
				Console.WriteLine(strAppName + " 80 100 32 \"path-to\\ethminer.exe\" -P stratum://0xETH_PUB_KEY:x@us2.ethermine.org:14444");
				Console.WriteLine("");
			}
		}


		static void MinerRunningLoop(CmdLineParams info)
		{
			//Loop that runs the miner and watches its performance

			Random rnd = new Random();

			for(;;)
			{
				int nmsWait;

				//Run the miner
				MinerRunResult res = RunMiner(info);

				if(res == MinerRunResult.RES_MR_BAD_PARAMS_DIDNT_RUN)
				{
					gEventLog.logMessage(EventLogMsgType.ELM_TYP_Critical, "Quitting watch app due to unrecoverable error!");
					OutputConsoleError("CRITICAL ERROR: Quitting watch app due to unrecoverable error!");
					return;
				}
				else if(res == MinerRunResult.RES_MR_EXCEPTION)
				{
					//Wait for some time
					nmsWait = rnd.Next(10 * 1000, 60 * 1000);

				}
				else if(res == MinerRunResult.RES_MR_MINER_EXITED)
				{
					//Wait for some time
					nmsWait = rnd.Next(10 * 1000, 30 * 1000);

				}
				else
				{
					//Some other value
					gEventLog.logMessage(EventLogMsgType.ELM_TYP_Critical, "Quitting watch app due to unknown error! err=" + res);
					OutputConsoleError("CRITICAL ERROR: Quitting watch app due to unknown error!");
					return;
				}	


				//Wait before restarting
				Console.WriteLine("Waiting for " + nmsWait/1000 + " sec before restarting the miner...");
				Thread.Sleep(nmsWait);

				//Log message
				int nExitCode;
				uint nNumStarted;
				gWS.getLastMinerExitTimeUTC(out nExitCode, out nNumStarted);
				gEventLog.logMessage(EventLogMsgType.ELM_TYP_Warning, "MINER RESTART (number " + nNumStarted + ") after " + nmsWait/1000 + " sec delay. Reason=" + res + 
					", exitCode=0x" + nExitCode.ToString("X"));


				//See if we've restarted too many times (and if we're allowed to reboot)
				if(info.nMaxAllowedMinerRestartsBeforeReboot > 0 &&
					nNumStarted > info.nMaxAllowedMinerRestartsBeforeReboot)
				{
					//Need to reboot the system
					gEventLog.logMessage(EventLogMsgType.ELM_TYP_Error, "Will attempt to REBOOT the rig after " + nNumStarted + " miner restarts");
					OutputConsoleError("CRITICAL: Will attempt to REBOOT the rig after " + nNumStarted + " miner restarts");

					Thread.Sleep(3 * 1000);

					//Force reboot
					rebootRig(true, "After " + nNumStarted + "attempts to restart miner process");

					break;
				}
			}

		}


		public enum MinerRunResult
		{
			RES_MR_BAD_PARAMS_DIDNT_RUN,		//Bad command line parameters - didn't run the miner at all
			RES_MR_EXCEPTION,					//Exception in this module while trying to run the miner
			RES_MR_MINER_EXITED,				//Miner has exited
		}


		static MinerRunResult RunMiner(CmdLineParams info)
		{
			//Run the miner process and begin watching it
			MinerRunResult res = MinerRunResult.RES_MR_BAD_PARAMS_DIDNT_RUN;

			try
			{
				Process proc = new Process();
				proc.StartInfo.FileName = info.strMinerExePath;

				if(info.arrMinerCmdParams.Count > 0)
				{
					//Make command line
					string strCmdLn = "";

					foreach(string strCmd in info.arrMinerCmdParams)
					{
						if(!string.IsNullOrEmpty(strCmdLn))
							strCmdLn += " ";

						if(strCmd.IndexOf(' ') == -1)
						{
							strCmdLn += strCmd;
						}
						else
						{
							strCmdLn += "\"" + strCmd + "\"";
						}
					}

					proc.StartInfo.Arguments = strCmdLn;
					proc.StartInfo.UseShellExecute = false;
					proc.StartInfo.CreateNoWindow = true;

					proc.StartInfo.RedirectStandardOutput = true;
					proc.StartInfo.RedirectStandardError = true;

					proc.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
					{
						try
						{
							DataReceivedFromMiner(e.Data, info);
						}
						catch(Exception ex)
						{
							//Failed
							gEventLog.logMessage(EventLogMsgType.ELM_TYP_Error, "EXCEPTION_2: " + ex.ToString());
							OutputConsoleError("EXCEPTION_2: " + ex.ToString());
						}
					});

					proc.ErrorDataReceived += new DataReceivedEventHandler((sender, e) =>
					{
						try
						{
							DataReceivedFromMiner(e.Data, info);
						}
						catch(Exception ex)
						{
							//Failed
							gEventLog.logMessage(EventLogMsgType.ELM_TYP_Error, "EXCEPTION_3: " + ex.ToString());
							OutputConsoleError("EXCEPTION_3: " + ex.ToString());
						}
					});


					//Start the process
					gWS.setMinerProcessClass(proc, true);
					proc.Start();

					//Make the miner process exit with ours
					AttachChildProcessToThisProcess(proc);


					int nPID = proc.Id;
					gEventLog.logMessage(EventLogMsgType.ELM_TYP_Information, "Miner started (PID=" + nPID + ") ... with CMD: " + strCmdLn);

					proc.BeginErrorReadLine();
					proc.BeginOutputReadLine();
					proc.WaitForExit();

					//Get exit code & remember it
					uint nExitCd = (uint)proc.ExitCode;
					gWS.setLastMinerExitTimeUTC(nExitCd);

					gEventLog.logMessage(EventLogMsgType.ELM_TYP_Error, "Miner process (PID=" + nPID + ") has exited with error code 0x" + nExitCd.ToString("X"));

					OutputConsoleError("WARNING: Miner has exited with error code 0x" + nExitCd.ToString("X") + " ....");

					res = MinerRunResult.RES_MR_MINER_EXITED;
				}
				else
				{
					//Error
					OutputConsoleError("ERROR: Not enough parameters to start a miner");

					res = MinerRunResult.RES_MR_BAD_PARAMS_DIDNT_RUN;
				}
			}
			catch(Exception ex)
			{
				//Failed
				gEventLog.logMessage(EventLogMsgType.ELM_TYP_Error, "EXCEPTION_1: " + ex.ToString());
				OutputConsoleError("EXCEPTION_1: " + ex.ToString());
				res = MinerRunResult.RES_MR_EXCEPTION;
			}

			return res;
		}



		public enum JobObjectInfoType
		{
			AssociateCompletionPortInformation = 7,
			BasicLimitInformation = 2,
			BasicUIRestrictions = 4,
			EndOfJobTimeInformation = 6,
			ExtendedLimitInformation = 9,
			SecurityLimitInformation = 5,
			GroupInformation = 11
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct JOBOBJECT_BASIC_LIMIT_INFORMATION
		{
			public Int64 PerProcessUserTimeLimit;
			public Int64 PerJobUserTimeLimit;
			public JOBOBJECTLIMIT LimitFlags;
			public UIntPtr MinimumWorkingSetSize;
			public UIntPtr MaximumWorkingSetSize;
			public UInt32 ActiveProcessLimit;
			public Int64 Affinity;
			public UInt32 PriorityClass;
			public UInt32 SchedulingClass;
		}

		[Flags]
		public enum JOBOBJECTLIMIT : uint
		{
			JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct IO_COUNTERS
		{
			public UInt64 ReadOperationCount;
			public UInt64 WriteOperationCount;
			public UInt64 OtherOperationCount;
			public UInt64 ReadTransferCount;
			public UInt64 WriteTransferCount;
			public UInt64 OtherTransferCount;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
		{
			public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
			public IO_COUNTERS IoInfo;
			public UIntPtr ProcessMemoryLimit;
			public UIntPtr JobMemoryLimit;
			public UIntPtr PeakProcessMemoryUsed;
			public UIntPtr PeakJobMemoryUsed;
		}


		[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string name);

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		static extern bool SetInformationJobObject(IntPtr job, JobObjectInfoType infoType,
		IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

		[DllImport("kernel32.dll", SetLastError = true)]
		static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

		static private IntPtr ghJob;



		static void AttachChildProcessToThisProcess(Process proc)
		{
			//Attach 'proc' process to this process, so that it's closed along with this process

			try
			{
				if (ghJob == IntPtr.Zero)
				{
					ghJob = CreateJobObject(IntPtr.Zero, "");		//It will be closed automatically when this process exits or is terminated

					if (ghJob == IntPtr.Zero)
					{
						throw new Win32Exception();
					}
				}

				JOBOBJECT_BASIC_LIMIT_INFORMATION info = new JOBOBJECT_BASIC_LIMIT_INFORMATION();
				info.LimitFlags = JOBOBJECTLIMIT.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

				JOBOBJECT_EXTENDED_LIMIT_INFORMATION exInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
				exInfo.BasicLimitInformation = info;

				int nLength = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
				IntPtr exInfoPtr = Marshal.AllocHGlobal(nLength);

				try
				{
					Marshal.StructureToPtr(exInfo, exInfoPtr, false);

					if (!SetInformationJobObject(ghJob, JobObjectInfoType.ExtendedLimitInformation, exInfoPtr, (uint)nLength))
					{
						throw new Win32Exception();
					}

					//And attach the process
					if (!AssignProcessToJobObject(ghJob, proc.Handle))
					{
						throw new Win32Exception();
					}
				}
				finally
				{
					Marshal.FreeHGlobal(exInfoPtr);
				}

			}
			catch(Exception ex)
			{
				//Error
				gEventLog.logMessage(EventLogMsgType.ELM_TYP_Error, "Failed to assign miner job: " + ex.ToString());
				OutputConsoleError("ERROR: Failed to assign miner job: " + ex.ToString());
			}
		}

		static void OutputConsoleError(string strMsg)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine(strMsg);
			Console.ForegroundColor = ConsoleColor.White;
		}




		class FindAndColorText
		{
			public string strText;
			public bool bIgnoreCase;
			public ConsoleColor clr;
		}

		static List<FindAndColorText> gArrClrText = new List<FindAndColorText>()
		{
			new FindAndColorText(){ strText = "**Accepted", bIgnoreCase=false, clr = ConsoleColor.Green},
			new FindAndColorText(){ strText = "Job:", bIgnoreCase=false, clr = ConsoleColor.Blue},
			new FindAndColorText(){ strText = "Mh", bIgnoreCase=false, clr = ConsoleColor.Cyan},
		};

		static void DataReceivedFromMiner(string strData, CmdLineParams info)
		{
			//Process data received from the miner

			if(string.IsNullOrEmpty(strData))
				return;

			//Try to colorize text received
			SortedDictionary<int, FindAndColorText> dicClrs = new SortedDictionary<int, FindAndColorText>();

			foreach(FindAndColorText fac in gArrClrText)
			{
				for (int i = 0; ;)
				{
					int nFnd = strData.IndexOf(fac.strText, i, fac.bIgnoreCase ? StringComparison.CurrentCultureIgnoreCase : StringComparison.CurrentCulture);
					if (nFnd == -1)
					{
						break;
					}

					dicClrs[nFnd] = fac;

					i = nFnd + fac.strText.Length;
				}
			}


			//Sort by index
			var sorted = dicClrs.ToList();
			int j = 0;

			//And output on the console with color
			foreach (var kvp in sorted)
			{
				int nInd = kvp.Key;
				Console.Write(strData.Substring(j, nInd - j));

				int nLn = kvp.Value.strText.Length;

				Console.ForegroundColor = kvp.Value.clr;
				Console.Write(strData.Substring(nInd, nLn));
				Console.ForegroundColor = ConsoleColor.White;

				j = nInd + nLn;
			}

			if(j < strData.Length)
			{
				Console.Write(strData.Substring(j));
			}

			Console.WriteLine("");


			//Then analyze the data received from the miner
			AnalyzeDataReceivedFromMiner(strData, info);
		}


		static void AnalyzeDataReceivedFromMiner(string strData, CmdLineParams info)
		{
			//Tokenzine by space
			string[] arrPs = strData.Trim().Split(' ');
			int nCnt = arrPs.Count();

			// i 16:08:18 <unknown> Job: 23a95ff1... us2.ethermine.org [172.65.226.101:14444]
			// i 16:08:18 <unknown> Job: 4e9718f8... us2.ethermine.org [172.65.226.101:14444]
			// i 16:08:22 <unknown> Job: 03e90dfb... us2.ethermine.org [172.65.226.101:14444]
			// m 16:08:22 <unknown> 30:02 A2334:R4 90.96 Mh - cu0 22.65, cu1 22.77, cu2 22.77, cu3 22.77
			// i 16:08:26 <unknown> Job: cea10285... us2.ethermine.org [172.65.226.101:14444]
			// m 16:08:27 <unknown> 30:02 A2334:R4 90.90 Mh - cu0 22.58, cu1 22.77, cu2 22.77, cu3 22.77
			// i 16:08:30 <unknown> Job: e8275758... us2.ethermine.org [172.65.226.101:14444]
			// m 16:08:33 <unknown> 30:02 A2334:R4 90.97 Mh - cu0 22.65, cu1 22.77, cu2 22.77, cu3 22.77
			// i 16:08:34 <unknown> Job: 78de40c4... us2.ethermine.org [172.65.226.101:14444]
			// i 16:08:37 <unknown> Job: 3884b399... us2.ethermine.org [172.65.226.101:14444]
			// i 16:08:37 <unknown> Job: 894baba6... us2.ethermine.org [172.65.226.101:14444]
			// m 16:08:38 <unknown> 30:02 A2334:R4 90.96 Mh - cu0 22.65, cu1 22.77, cu2 22.77, cu3 22.77
			// i 16:08:41 <unknown> Job: 4c86d1d0... us2.ethermine.org [172.65.226.101:14444]
			// m 16:08:43 <unknown> 30:02 A2334:R4 90.90 Mh - cu0 22.58, cu1 22.77, cu2 22.77, cu3 22.77
			// i 16:08:43 <unknown> Job: cc9d4a34... us2.ethermine.org [172.65.226.101:14444]
			// i 16:08:43 <unknown> Job: 4c34a4ad... us2.ethermine.org [172.65.226.101:14444]
			// i 16:08:47 <unknown> Job: cc8dced9... us2.ethermine.org [172.65.226.101:14444]
			// m 16:08:48 <unknown> 30:03 A2334:R4 90.97 Mh - cu0 22.65, cu1 22.77, cu2 22.77, cu3 22.77
			//cu 16:08:48 cuda-1   Job: cc8dced9... Sol: 0xbca14f007c4cf6d7
			// i 16:08:48 <unknown> **Accepted  27 ms. us2.ethermine.org [172.65.226.101:14444]
			// i 16:08:50 <unknown> Job: 24773f74... us2.ethermine.org [172.65.226.101:14444]
			// i 16:08:50 <unknown> Job: b7631f18... us2.ethermine.org [172.65.226.101:14444]
			// m 16:08:53 <unknown> 30:03 A2335:R4 90.90 Mh - cu0 22.59, cu1 22.77, cu2 22.77, cu3 22.77
			//cu 16:08:54 cuda-2   Job: b7631f18... Sol: 0xbca14f017f7427cb
			// i 16:08:54 <unknown> **Accepted  26 ms. us2.ethermine.org [172.65.226.101:14444]
			// i 16:08:54 <unknown> Job: 8a7cbba8... us2.ethermine.org [172.65.226.101:14444]


			if (nCnt > 0 &&
				(arrPs[0].CompareTo("m") == 0 || arrPs[0].CompareTo("i") == 0))
			{
				for (int i = 1; i < nCnt; i++) {
                    double fHashRate;
                    if (arrPs[i].Contains("Mh") &&  //Fall through instead of nested IFs
                        i - 2 > 0 &&
                        double.TryParse(arrPs[i - 1], out fHashRate) &&
                        arrPs[i - 2].IndexOf('A') == 0) {
                        if (fHashRate >= info.nHashRangeMin && fHashRate <= info.nHashRangeMax) {
                            gWS.setLastGoodHashRateTimeUTC();
                        }
                        else if (fHashRate >= info.nHashRangeMin && info.nHashRangeMax == 0) {  //max was set to 0 only check min
                            gWS.setLastGoodHashRateTimeUTC();
                        }
                        break;
                    }
                    else if (arrPs[i].CompareTo("**Accepted") == 0) {
                        //Hash was accepted
                        gWS.setLastAcceptedTimeUTC();

                        break;
                    }
                }
			}

		}


		private static string gkstrEmailSubjectMinerCriticalState = "MINER CRITICAL STATE - NEED MANUAL INTERACTION";

		public static void rebootRig(bool bForce, string strReason)
		{
			//Reboot the rig
			//'bForce' = true to force it (and lose unsaved data)
			//'strReason' = short reason for rebooting

			try
			{
				string strProcName;
#if DEBUG
				strProcName = "something.exe";		//Just for testing
#else
				strProcName = "shutdown.exe";
#endif
				//Send noticiation via email
				sendEmailNotification("Rebooting ETH Miner", "Reason: " + strReason);

				//Initiate a reboot
				Process proc = Process.Start(strProcName, "/r " + (bForce ? "/f " : "") + "/t 0");

				if(proc.WaitForExit(10 * 1000))
				{
					//Did it
					gEventLog.logMessage(EventLogMsgType.ELM_TYP_Warning, "Initiated a REBOOT, exitCode=" + proc.ExitCode + " , reason: " + strReason);
				}
				else
				{
					//Timed out
					gEventLog.logMessage(EventLogMsgType.ELM_TYP_Critical, "Timed out while attempting to REBOOT! Reason: " + strReason);

					//Send notification
					sendEmailNotification(gkstrEmailSubjectMinerCriticalState, "Rig timed out to reboot. Reason: " + strReason);

					//And quit self
					Process.GetCurrentProcess().Kill();
				}
			}
			catch(Exception ex)
			{
				gEventLog.logMessage(EventLogMsgType.ELM_TYP_Critical, "Failed to REBOOT, reason: " + strReason + ". " + ex.ToString());

				//Send notification
				sendEmailNotification(gkstrEmailSubjectMinerCriticalState, "Exception while trying to reboot rig. Reason: " + strReason);
			}
		}

		public static void sendEmailNotification(string strSubject, string strMsg)
		{
			//Send an email to self with a critical rig state notification
			//'strSubject' = short subject line for the email
			//'strMsg' = short message to include (will have current time added to it)

			try
			{
				strMsg = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] " + strMsg;

				Task.Run(() => task_NotifyWebServer(strSubject, strMsg));
			}
			catch(Exception ex)
			{
				//Exception
				gEventLog.logMessage(EventLogMsgType.ELM_TYP_Critical, "EXCEPTION in sendEmailNotification: " + ex.ToString());
			}
		}

		static async Task task_NotifyWebServer(string strSubj, string strMsg)
		{
			//Only if we have the notification key
			if (!string.IsNullOrEmpty(gkstrNotificationKey))
			{
				try
				{
					using (HttpClient httpClient = new HttpClient())
					{
						//10-second timeout
						httpClient.Timeout = TimeSpan.FromSeconds(10);
						httpClient.BaseAddress = new Uri(gkstrNotificationHost);

						//For the sample of PHP code on the server, see the top of this document
						var content = new FormUrlEncodedContent(new[] {
							new KeyValuePair<string, string>("subj", strSubj),
							new KeyValuePair<string, string>("msg", strMsg),
							new KeyValuePair<string, string>("key", gkstrNotificationKey),
						});

						var result = await httpClient.PostAsync(gkstrNotificationPage, content);

						//Get result
						string strResponse = await result.Content.ReadAsStringAsync();
						int nStatusCode = (int)result.StatusCode;

						if (nStatusCode != 200)
						{
							//Something went wrong
							gEventLog.logMessage(EventLogMsgType.ELM_TYP_Critical, "Failed to send web server notification. Code: " + nStatusCode + ", SUBJ: " + strSubj + ", MSG: " + strMsg);
						}
					}

				}
				catch (Exception ex)
				{
					gEventLog.logMessage(EventLogMsgType.ELM_TYP_Critical, "EXCEPTION in task_NotifyWebServer: " + ex.ToString());
				}
			}
		}


		[System.Runtime.InteropServices.DllImport("kernel32.dll")]
		private static extern long GetTickCount64();

		public static DateTime GetLastBootTime()
		{
			//RETURN:
			//		= Local date and time when system was last booted
			return DateTime.Now - new TimeSpan(GetTickCount64() * 10000);
		}

		public static bool IsProcessRunning(Process proc)
		{
			//RETURN: = true if 'proc' is currently running
			try
			{
				Process p = Process.GetProcessById(proc.Id);
				if(p != null)
				{
					return true;
				}
			}
			catch (Exception) 
			{
				//No such
			}

			return false;
		}

		private static void threadWatchMiner(CmdLineParams cmd)
		{
			//Worker thread that watches the miner output
			//'cmd' = command line parameters

			Random rnd = new Random();

			DateTime dtmUtcLastNotRunning = DateTime.MinValue;
			DateTime dtmUtcLastStatsShown = DateTime.UtcNow;


			for(;; System.Threading.Thread.Sleep(500))
			{
				bool bMinerRunning = false;
				TimeSpan spnProcLifetime = new TimeSpan();

				//Is miner running?
				Process procMiner = gWS.getMinerProcessClass();
				if (procMiner != null &&
					IsProcessRunning(procMiner))
				{
					//Miner is running
					dtmUtcLastNotRunning = DateTime.MinValue;
					bMinerRunning = true;

					//How long ago did the miner process start running?
					DateTime dtmStarted = procMiner.StartTime;

					//How long ago was it?
					spnProcLifetime = DateTime.Now - dtmStarted;


					//Only if been running for 40 seconds
					//INFO: The miner will be passing its own initialization, so skip it ....
					double fSecRan = spnProcLifetime.TotalSeconds;
					if(fSecRan > 40.0)
					{
						int nPID = procMiner.Id;
						int nSleepDelaySec;

						double fSecSinceLastGH;
						double fSecKillAfter;

						//Get last time we had a good hash rate
						DateTime dtmUtc_LastGHR = gWS.getLastGoodHashRateTimeUTC();
						if (dtmUtc_LastGHR != DateTime.MinValue)
						{
							TimeSpan spnSinceLGH = DateTime.UtcNow - dtmUtc_LastGHR;
							fSecSinceLastGH = spnSinceLGH.TotalSeconds;
							fSecKillAfter = 60.0 * 2;		//When to restart miner - if no good hash rate after
						}
						else
						{
							//Never before
							fSecSinceLastGH = fSecRan;
							fSecKillAfter = 60.0 * 4;		//When to restart miner - if no good hash rate after
						}


						//See if we need to kill the miner process
						if(fSecSinceLastGH > fSecKillAfter)
						{
							//Kill the miner
							try
							{
								gEventLog.logMessage(EventLogMsgType.ELM_TYP_Warning, "Will attempt to kill miner (PID=" + nPID + ") after no good hash rates for " + fSecKillAfter + " sec");
								OutputConsoleError("Will attempt to kill miner (PID=" + nPID + ") after no good hash rates for " + fSecKillAfter + " sec");

								procMiner.Kill();

								//Reset counters
								gWS.setLastGoodHashRateTimeUTC();
								gWS.setLastMinerExitTimeUTC(0xdead0001);

								nSleepDelaySec = rnd.Next(1000, 3000);
							}
							catch(Exception ex)
							{
								//Failed
								gEventLog.logMessage(EventLogMsgType.ELM_TYP_Critical, "Failed to kill miner (PID=" + nPID + ") after no good hash rates -- " + ex.ToString());
								OutputConsoleError("ERROR: Kill miner process failed! Check the log for details...");

								nSleepDelaySec = rnd.Next(3000, 10000);
							}

							//Wait a little
							Thread.Sleep(nSleepDelaySec);

							continue;
						}



						//Get last time we had an accepted hash
						double fSecSinceLastAcc;
						DateTime dtmUtc_LastAccept = gWS.getLastAccaptedTimeUTC();
						if (dtmUtc_LastAccept != DateTime.MinValue)
						{
							TimeSpan spnSinceLAcc = DateTime.UtcNow - dtmUtc_LastAccept;
							fSecSinceLastAcc = spnSinceLAcc.TotalSeconds;
							fSecKillAfter = 60.0 * 30;		//When to restart miner - if no good hash rate after
						}
						else
						{
							//Never before
							fSecSinceLastAcc = fSecRan;
							fSecKillAfter = 60.0 * 35;		//When to restart miner - if no good hash rate after
						}

						//See if we need to kill the miner process
						if(fSecSinceLastAcc > fSecKillAfter)
						{
							//Kill the miner
							try
							{
								gEventLog.logMessage(EventLogMsgType.ELM_TYP_Warning, "Will attempt to kill miner (PID=" + nPID + ") after no accepted hashes for " + fSecKillAfter + " sec");
								OutputConsoleError("Will attempt to kill miner (PID=" + nPID + ") after no accepted hashes for " + fSecKillAfter + " sec");

								procMiner.Kill();

								//Reset counters
								gWS.setLastAcceptedTimeUTC();
								gWS.setLastMinerExitTimeUTC(0xdead0002);

								nSleepDelaySec = rnd.Next(1000, 3000);
							}
							catch(Exception ex)
							{
								//Failed
								gEventLog.logMessage(EventLogMsgType.ELM_TYP_Critical, "Failed to kill miner (PID=" + nPID + ") after no accepted hashes -- " + ex.ToString());
								OutputConsoleError("ERROR: Kill miner process failed! Check the log for details...");

								nSleepDelaySec = rnd.Next(3000, 10000);
							}

							//Wait a little
							Thread.Sleep(nSleepDelaySec);

							continue;
						}



					}
				}
				else
				{
					//Miner is not running
					if(dtmUtcLastNotRunning != DateTime.MinValue)
					{
						//See how long the miner wasn't running
						TimeSpan spnNotRunning = DateTime.UtcNow - dtmUtcLastNotRunning;
						double fSecNotRunning = spnNotRunning.TotalSeconds;

						//Check if miner was not running for too long
						if(fSecNotRunning > (5 * 60.0))		//5 minutes
						{
							//Need to reboot
							gEventLog.logMessage(EventLogMsgType.ELM_TYP_Error, "Will attempt to REBOOT the rig after " + fSecNotRunning + " sec of miner process not running");
							OutputConsoleError("CRITICAL: Will attempt to REBOOT the rig after " + fSecNotRunning + " sec of miner process not running");

							Thread.Sleep(3 * 1000);

							//Force reboot
							rebootRig(true, "After " + fSecNotRunning + " of miner process not running");

							//Reset counter
							dtmUtcLastNotRunning = DateTime.MinValue;
						}
					}
					else
					{
						dtmUtcLastNotRunning = DateTime.UtcNow;
					}
				}



				//See if we need to show stats
				DateTime dtmNowUtc = DateTime.UtcNow;
				double fSecSinceLastStats = (dtmNowUtc - dtmUtcLastStatsShown).TotalSeconds;

				//Show it every N seconds
				if(fSecSinceLastStats > 15.0)
				{
					dtmUtcLastStatsShown = dtmNowUtc;

					const string strFmtTimeSpan = "hh\\:mm\\:ss";

					//Output watch stats
					string strStats = "WATCH: Miner=" + (bMinerRunning ? "On" : "OFF");

					if (bMinerRunning)
					{
						strStats += " Runtime=";
						strStats += spnProcLifetime.ToString("dd\\:hh\\:mm\\:ss");
					}

					int nExitCode;
					uint nNumRunning;
					gWS.getLastMinerExitTimeUTC(out nExitCode, out nNumRunning);
					strStats += " Restarts=" + (nNumRunning - 1);

					strStats += " LastAccepted=";
					DateTime dtmLastAcceptedUTC = gWS.getLastAccaptedTimeUTC();
					if(dtmLastAcceptedUTC != DateTime.MinValue)
					{
						strStats += (dtmNowUtc - dtmLastAcceptedUTC).ToString(strFmtTimeSpan);
					}
					else
						strStats += "Never";


					strStats += " LastGoodHash=";
					DateTime dtmLastGoodHashUTC = gWS.getLastGoodHashRateTimeUTC();
					if(dtmLastGoodHashUTC != DateTime.MinValue)
					{
						strStats += (dtmNowUtc - dtmLastGoodHashUTC).ToString(strFmtTimeSpan);
					}
					else
						strStats += "Never";



					//Output
					Console.ForegroundColor = ConsoleColor.DarkYellow;
					Console.WriteLine(strStats);
					Console.ForegroundColor = ConsoleColor.White;
				}
			}

		}


	}
}
