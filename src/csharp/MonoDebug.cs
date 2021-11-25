/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace VSCodeDebug
{
	internal class Program
	{
		const int DEFAULT_PORT = 4711;

		private static bool trace_requests;
		private static bool trace_responses;
		static string LOG_FILE_PATH = null;

		public static string WorkspaceFolder { get; private set; }

		private static void Main(string[] argv)
		{
			int port = -1;

			// parse command line arguments
			foreach (var a in argv) {
				switch (a) {
				case "--trace":
					trace_requests = true;
					break;
				case "--trace=response":
					trace_requests = true;
					trace_responses = true;
					break;
				case "--server":
					port = DEFAULT_PORT;
					break;
				default:
					if (a.StartsWith("--server=")) {
						if (!int.TryParse(a.Substring("--server=".Length), out port)) {
							port = DEFAULT_PORT;
						}
					}
					else if( a.StartsWith("--log-file=")) {
						LOG_FILE_PATH = a.Substring("--log-file=".Length);
					}
					else if (a.StartsWith("--workspaceFolder=")) {
						WorkspaceFolder = a.Substring("--workspaceFolder=".Length);
					}
					break;
				}
			}

			if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("rhino_debug_logfile")) == false) {
				LOG_FILE_PATH = Environment.GetEnvironmentVariable("rhino_debug_logfile");
			}
			if (EnsureLogFile())
			{
				Trace.Listeners.Add(new TextWriterTraceListener(logFile));
			}
	
			if (port > 0) {
				// TCP/IP server
				Program.Log("waiting for debug protocol on port " + port);
				RunServer(port);
			} else {
				// stdin/stdout
				Program.Log("waiting for debug protocol on stdin/stdout");
				RunSession(Console.OpenStandardInput(), Console.OpenStandardOutput());
			}
		}

		static TextWriter logFile;

		public static void Log(bool predicate, string msg)
		{
			if (predicate)
			{
				Log(msg);
			}
		}
		
		public static void Log(string msg)
		{
			try
			{
				Console.Error.WriteLine(msg);

				if (LOG_FILE_PATH != null)
                {
                    EnsureLogFile();

                    logFile.WriteLine(string.Format("{0} {1}", DateTime.UtcNow.ToLongTimeString(), msg));
                    logFile.Flush();
                }
            }
			catch (Exception ex)
			{
				if (LOG_FILE_PATH != null)
				{
					try
					{
						File.WriteAllText(LOG_FILE_PATH + ".err", ex.ToString());
					}
					catch
					{
					}
				}

				throw;
			}
		}

        private static bool EnsureLogFile()
        {
			if (logFile != null)
				return true;

			if (LOG_FILE_PATH == null)
				return false;
				
			var file = File.CreateText(LOG_FILE_PATH);
			file.AutoFlush = true;
			logFile = file;
			return true;
        }

        private static void RunSession(Stream inputStream, Stream outputStream)
		{
			DebugSession debugSession = new MonoDebugSession();
			debugSession.TRACE = trace_requests;
			debugSession.TRACE_RESPONSE = trace_responses;
			debugSession.Start(inputStream, outputStream).Wait();

			if (logFile!=null)
			{
				logFile.Flush();
				logFile.Close();
				logFile = null;
			}
		}

		private static void RunServer(int port)
		{
			TcpListener serverSocket = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
			serverSocket.Start();

			new System.Threading.Thread(() => {
				while (true) {
					var clientSocket = serverSocket.AcceptSocket();
					if (clientSocket != null) {
						Program.Log(">> accepted connection from client");

						new System.Threading.Thread(() => {
							using (var networkStream = new NetworkStream(clientSocket)) {
								try {
									RunSession(networkStream, networkStream);
								}
								catch (Exception e) {
									Console.Error.WriteLine("Exception: " + e);
								}
							}
							clientSocket.Close();
							Console.Error.WriteLine(">> client connection closed");
						}).Start();
					}
				}
			}).Start();
		}
	}
}
