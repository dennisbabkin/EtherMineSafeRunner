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
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EtherMineSafeRunner
{
	class CmdLineParams
	{
		public uint nHashRangeMin = 0;									//Minimum allowed hash rate in Mh
		public uint nHashRangeMax = 0;									//[If not 0] maximum allowed hash rate in Mh (otherwise not used if 0)
		public uint nMaxAllowedMinerRestartsBeforeReboot = 32;			//Max number of attempts to restart the miner process before rebooting the system

		public string strMinerExePath = "";
		public List<string> arrMinerCmdParams = new List<string>();
	}


	class WatchState
	{
		private DateTime dtmUtc_LastAccepted = DateTime.MinValue;       //When a hash was last accepted as UTC time, or DateTime.MinValue if none
		private object _lock_dtmUtc_LastAccepted = new object();

		public DateTime getLastAccaptedTimeUTC()
		{
			//RETURN:
			//      = Last UTC time hash was accepted, or
			//      = DateTime.MinValue if never happened
			lock (_lock_dtmUtc_LastAccepted)
			{
				return dtmUtc_LastAccepted;
			}
		}

		public void setLastAcceptedTimeUTC()
		{
			//Set UTC time now as the time hash was accepted
			lock (_lock_dtmUtc_LastAccepted)
			{
				dtmUtc_LastAccepted = DateTime.UtcNow;
			}
		}


		private DateTime dtmUtc_LastGoodHashRate = DateTime.MinValue;     //[UTC] when last good hash was detected, or DateTime.MinValue if none
		private object _lock_LastGoodHashRate = new object();

		public DateTime getLastGoodHashRateTimeUTC()
		{
			//RETURN:
			//      = Last UTC time good hash rate was noticed, or
			//      = DateTime.MinValue if never happened
			lock (_lock_LastGoodHashRate)
			{
				return dtmUtc_LastGoodHashRate;
			}
		}

		public void setLastGoodHashRateTimeUTC()
		{
			//Set UTC time now as the time last good hash rate was noticed
			lock (_lock_LastGoodHashRate)
			{
				dtmUtc_LastGoodHashRate = DateTime.UtcNow;
			}
		}


		private DateTime dtmUtc_LastMinerExit = DateTime.MinValue;      //[UTC] when last time the miner app has exited (meaning, it probably crashed)
		private int nExitCode_Miner = 0;                                //Exit code the miner has exited with
		private uint nCountMinerStarted = 0;                            //Number of times the miner was started
		private object _lock_LastMinerExit = new object();

		public DateTime getLastMinerExitTimeUTC(out int nOutExitCode, out uint nOutCntMinerStarted)
		{
			//RETURN:
			//      = Last UTC time the miner has exited (meaning, it probably crashed)
			//			INFO: In this case `nOutExitCode` receives the exit code from the miner,
			//					'nOutCntMinerStarted' receives the number of times the miner was started
			//      = DateTime.MinValue if it never happened
			lock (_lock_LastMinerExit)
			{
				nOutCntMinerStarted = nCountMinerStarted;
				nOutExitCode = nExitCode_Miner;

				return dtmUtc_LastMinerExit;
			}
		}

		public void setLastMinerExitTimeUTC(uint nMinerExitCode)
		{
			//Set UTC time now as the time miner has exited
			lock (_lock_LastGoodHashRate)
			{
				lock (_lock_procMiner)
				{
					procMiner = null;
					dtmUtc_LastMinerExit = DateTime.UtcNow;
					nExitCode_Miner = (int)nMinerExitCode;
				}
			}
		}



		private Process procMiner = null;                               //Miner process, or null if it's not set yet
		private object _lock_procMiner = new object();

		public Process getMinerProcessClass()
		{
			//RETURN:
			//		= process class for the running miner
			//		= null if miner is not running
			lock (_lock_procMiner)
			{
				return procMiner;
			}
		}

		public void setMinerProcessClass(Process proc, bool bIncrementMinerStartCounter)
		{
			//Set process for the running miner, or null if none
			lock (_lock_procMiner)
			{
				if (bIncrementMinerStartCounter)
				{
					lock (_lock_LastMinerExit)
					{
						nCountMinerStarted++;
					}
				}

				procMiner = proc;
			}
		}



		private DateTime dtmUtc_ThisAppStarted = DateTime.MinValue;         //[UTC] time when this monitoring app has started, or DateTime.MinValue if not known
		private object _lock_ThisAppStarted = new object();

		public DateTime getWhenThisAppStartedTimeUTC()
		{
			//RETURN:
			//		= [UTC] date/time when this app was started
			//		= DateTime.MinValue if not known yet
			lock (_lock_ThisAppStarted)
			{
				return dtmUtc_ThisAppStarted;
			}
		}

		public void setWhenThisAppStartedTimeUTC()
		{
			lock (_lock_ThisAppStarted)
			{
				dtmUtc_ThisAppStarted = DateTime.UtcNow;
			}
		}




	}




}
