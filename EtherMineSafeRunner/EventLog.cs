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
using System.IO;


//Class that provides logging of diagnostic events into a text file

namespace EtherMineSafeRunner
{
	enum EventLogMsgType
	{
		ELM_TYP_Information,
		ELM_TYP_Warning,
		ELM_TYP_Error,
		ELM_TYP_Critical,
	}


	class EventLog
	{
		private string _strLogFilePath = "EthMineSafeRunner_Log.txt";		//File to save the text event log to (you may provide a full file path, or just a file name)
		private long _ncbMaxAllowedFileSz = 2 * 1024 * 1024;				//2 MB = maximum allowed event log file size in bytes (old records will be removed if the file grows over this limit)

		private object _lock = new object();

		public EventLog()
		{

			//Check the size of log
			lock(_lock)
			{
				try
				{
					long nLength = new System.IO.FileInfo(_strLogFilePath).Length;
					if(nLength > _ncbMaxAllowedFileSz)
					{
						//Need to truncate it
						string strData = File.ReadAllText(_strLogFilePath);

						byte[] bytesData = Encoding.UTF8.GetBytes(strData);
						if (bytesData.Length > _ncbMaxAllowedFileSz)
						{
							byte[] bytesNewData = new byte[_ncbMaxAllowedFileSz];

							Array.Copy(bytesData, bytesData.Length - _ncbMaxAllowedFileSz, bytesNewData, 0, _ncbMaxAllowedFileSz);

							string strNewData = "";

							try
							{
								strNewData = System.Text.Encoding.UTF8.GetString(bytesNewData);
							}
							catch(Exception ex)
							{
								//Something went wrong
								strNewData = "<<Error - failed to truncate log - lost all data - please notify us at dennisbabkin.com/contact >>\r\n" + 
									"Error Description Follows:\r\n" + ex.ToString() + "\r\n\r\n";
							}

							//Write to file
							File.WriteAllText(_strLogFilePath, strNewData, Encoding.UTF8);
						}
					}
				}
				catch(FileNotFoundException)
				{
					//No such file
				}
			}
		}



		public bool logMessage(EventLogMsgType type, string strMessage)
		{
			bool bRes = false;

			try
			{
				//Make string to log
				string strMsg = "";

				switch (type)
				{
					case EventLogMsgType.ELM_TYP_Information:
						strMsg += "> ";
						break;
					case EventLogMsgType.ELM_TYP_Warning:
						strMsg += "Warning> ";
						break;
					case EventLogMsgType.ELM_TYP_Error:
						strMsg += "*ERROR*> ";
						break;
					case EventLogMsgType.ELM_TYP_Critical:
						strMsg += "**CRITICAL**> ";
						break;
				}

				//Add local time
				strMsg += DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

				//Message
				strMsg += ": " + strMessage;

				//New line
				strMsg += "\r\n";


				lock (_lock)
				{
					try
					{
						File.AppendAllText(_strLogFilePath, strMsg, Encoding.UTF8);
					}
					catch (Exception ex)
					{
#if DEBUG
						throw new Exception("Logging failed: " + ex.ToString());
#else
						bRes = ex != null && false;
#endif
					}
				}
			}
			catch(Exception ex)
			{
#if DEBUG
				throw new Exception("Logging failed in general: " + ex.ToString());
#else
				bRes = ex != null && false;
#endif
			}

			return bRes;
		}

	}
}
