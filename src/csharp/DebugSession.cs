﻿/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;


namespace VSCodeDebug
{
	// ---- Types -------------------------------------------------------------------------

	public class Message
	{
		public int id { get; }
		public string format { get; }
		public dynamic variables { get; }
		public dynamic showUser { get; }
		public dynamic sendTelemetry { get; }

		public Message(int id, string format, dynamic variables = null, bool user = true, bool telemetry = false) {
			this.id = id;
			this.format = format;
			this.variables = variables;
			this.showUser = user;
			this.sendTelemetry = telemetry;
		}
	}

	public class StackFrame
	{
		public int id { get; }
		public Source source { get; }
		public int line { get; }
		public int column { get; }
		public string name { get; }
		public string presentationHint { get; }

		public StackFrame(int id, string name, Source source, int line, int column, string hint) {
			this.id = id;
			this.name = name;
			this.source = source;

			// These should NEVER be negative
			this.line = Math.Max(0, line);
			this.column = Math.Max(0, column);

			this.presentationHint = hint;
		}
	}

	public class Scope
	{
		public string name { get; }
		public int variablesReference { get; }
		public bool expensive { get; }

		public Scope(string name, int variablesReference, bool expensive = false) {
			this.name = name;
			this.variablesReference = variablesReference;
			this.expensive = expensive;
		}
	}

	public class Variable
	{
		public string name { get; }
		public string value { get; }
		public string type { get; }
		public int variablesReference { get; }

		public Variable(string name, string value, string type, int variablesReference = 0) {
			this.name = name;
			this.value = value;
			this.type = type;
			this.variablesReference = variablesReference;
		}
	}

	public class Thread
	{
		public int id { get; }
		public string name { get; }

		public Thread(int id, string name) {
			this.id = id;
			if (name == null || name.Length == 0) {
				this.name = string.Format("Thread #{0}", id);
			} else {
				this.name = name;
			}
		}
	}

	public class Source
	{
		public string name { get; }
		public string path { get; }
		public int sourceReference { get; }
		public string presentationHint { get; }

		public Source(string name, string path, int sourceReference, string hint) {
			this.name = name;
			this.path = path;
			this.sourceReference = sourceReference;
			this.presentationHint = hint;
		}
	}

	public class Breakpoint
	{
		public bool verified { get; }
		public int line { get; }

		public Breakpoint(bool verified, int line) {
			this.verified = verified;
			this.line = line;
		}
	}

	// ---- Events -------------------------------------------------------------------------

	public class InitializedEvent : Event
	{
		public InitializedEvent()
			: base("initialized") { }
	}

	public class StoppedEvent : Event
	{
		public StoppedEvent(int tid, string reasn, string txt = null)
			: base("stopped", new {
				threadId = tid,
				reason = reasn,
				text = txt
			}) { }
	}

	public class ExitedEvent : Event
	{
		public ExitedEvent(int exCode)
			: base("exited", new { exitCode = exCode } ) { }
	}

	public class TerminatedEvent : Event
	{
		public TerminatedEvent()
			: base("terminated") {	}
	}

	public class ThreadEvent : Event
	{
		public ThreadEvent(string reasn, int tid)
			: base("thread", new {
				reason = reasn,
				threadId = tid
			}) { }
	}

	public class OutputEvent : Event
	{
		public OutputEvent(string cat, string outpt)
			: base("output", new {
				category = cat,
				output = outpt
			}) { }
	}

	// ---- Response -------------------------------------------------------------------------

	public class Capabilities : ResponseBody {

		public bool supportsConfigurationDoneRequest;
		public bool supportsFunctionBreakpoints;
		public bool supportsConditionalBreakpoints;
		public bool supportsEvaluateForHovers;
		public bool supportsLogPoints;
		public dynamic[] exceptionBreakpointFilters;
	}

	public class ErrorResponseBody : ResponseBody {

		public Message error { get; }

		public ErrorResponseBody(Message error) {
			this.error = error;
		}
	}

	public class StackTraceResponseBody : ResponseBody
	{
		public StackFrame[] stackFrames { get; }
		public int totalFrames { get; }

		public StackTraceResponseBody(List<StackFrame> frames, int total) {
			stackFrames = frames.ToArray<StackFrame>();
			totalFrames = total;
		}
	}

	public class ScopesResponseBody : ResponseBody
	{
		public Scope[] scopes { get; }

		public ScopesResponseBody(List<Scope> scps) {
			scopes = scps.ToArray<Scope>();
		}
	}

	public class VariablesResponseBody : ResponseBody
	{
		public Variable[] variables { get; }

		public VariablesResponseBody(List<Variable> vars) {
			variables = vars.ToArray<Variable>();
		}
	}

	public class ThreadsResponseBody : ResponseBody
	{
		public Thread[] threads { get; }

		public ThreadsResponseBody(List<Thread> ths) {
			threads = ths.ToArray<Thread>();
		}
	}

	public class EvaluateResponseBody : ResponseBody
	{
		public string result { get; }
		public int variablesReference { get; }

		public EvaluateResponseBody(string value, int reff = 0) {
			result = value;
			variablesReference = reff;
		}
	}

	public class SetBreakpointsResponseBody : ResponseBody
	{
		public Breakpoint[] breakpoints { get; }

			public SetBreakpointsResponseBody(List<Breakpoint> bpts = null) {
			if (bpts == null)
				breakpoints = new Breakpoint[0];
			else
				breakpoints = bpts.ToArray<Breakpoint>();
		}
	}

	public class SetExpressionResponseBody : ResponseBody
	{
		public string value { get; }
		public SetExpressionResponseBody(string value)
		{
			this.value = value;
		}
	}

	// ---- The Session --------------------------------------------------------

	public abstract class DebugSession : ProtocolServer
	{
		private bool _clientLinesStartAt1 = true;
		private bool _clientPathsAreURI = true;


		public DebugSession()
		{
		}

		public void SendResponse(Response response, dynamic body = null)
		{
			if (body != null) {
				response.SetBody(body);
			}
			SendMessage(response);
		}

		public void SendErrorResponse(Response response, int id, string format, dynamic arguments = null, bool user = true, bool telemetry = false)
		{
			var msg = new Message(id, format, arguments, user, telemetry);
			var message = Utilities.ExpandVariables(msg.format, msg.variables);
			response.SetErrorBody(message, new ErrorResponseBody(msg));
			SendMessage(response);
		}

		protected override void DispatchRequest(string command, dynamic args, Response response)
		{
			if (args == null) {
				args = new { };
			}

			try {
				switch (command) {

				case "initialize":
					if (args.linesStartAt1 != null) {
						_clientLinesStartAt1 = (bool)args.linesStartAt1;
					}
					var pathFormat = (string)args.pathFormat;
					if (pathFormat != null) {
						switch (pathFormat) {
						case "uri":
							_clientPathsAreURI = true;
							break;
						case "path":
							_clientPathsAreURI = false;
							break;
						default:
							SendErrorResponse(response, 1015, "initialize: bad value '{_format}' for pathFormat", new { _format = pathFormat });
							return;
						}
					}
					Initialize(response, args);
					break;

				case "launch":
					Launch(response, args);
					break;

				case "attach":
					Attach(response, args);
					break;

				case "disconnect":
					Disconnect(response, args);
					break;

				case "next":
					Next(response, args);
					break;

				case "continue":
					Continue(response, args);
					break;

				case "stepIn":
					StepIn(response, args);
					break;

				case "stepOut":
					StepOut(response, args);
					break;

				case "pause":
					Pause(response, args);
					break;

				case "stackTrace":
					StackTrace(response, args);
					break;

				case "scopes":
					Scopes(response, args);
					break;

				case "variables":
					Variables(response, args);
					break;

				case "source":
					Source(response, args);
					break;

				case "threads":
					Threads(response, args);
					break;

				case "setExpression":
					SetExpression(response, args);
					break;

				case "setBreakpoints":
					SetBreakpoints(response, args);
					break;

				case "setFunctionBreakpoints":
					SetFunctionBreakpoints(response, args);
					break;

				case "setExceptionBreakpoints":
					SetExceptionBreakpoints(response, args);
					break;

				case "evaluate":
					Evaluate(response, args);
					break;

				default:
					SendErrorResponse(response, 1014, "unrecognized request: {_request}", new { _request = command });
					break;
				}
			}
			catch (Exception e) {
				SendErrorResponse(response, 1104, "error while processing request '{_request}' (exception: {_exception})", new { _request = command, _exception = e.Message });
			}

			if (command == "disconnect") {
				Stop();
			}
		}

		public abstract void Initialize(Response response, dynamic args);

		public abstract void Launch(Response response, dynamic arguments);

		public abstract void Attach(Response response, dynamic arguments);

		public abstract void Disconnect(Response response, dynamic arguments);

		public virtual void SetFunctionBreakpoints(Response response, dynamic arguments)
		{
		}

		public virtual void SetExceptionBreakpoints(Response response, dynamic arguments)
		{
		}

		public abstract void SetExpression(Response response, dynamic arguments);

		public abstract void SetBreakpoints(Response response, dynamic arguments);

		public abstract void Continue(Response response, dynamic arguments);

		public abstract void Next(Response response, dynamic arguments);

		public abstract void StepIn(Response response, dynamic arguments);

		public abstract void StepOut(Response response, dynamic arguments);

		public abstract void Pause(Response response, dynamic arguments);

		public abstract void StackTrace(Response response, dynamic arguments);

		public abstract void Scopes(Response response, dynamic arguments);

		public abstract void Variables(Response response, dynamic arguments);

		public abstract void Source(Response response, dynamic arguments);

		public abstract void Threads(Response response, dynamic arguments);

		public abstract void Evaluate(Response response, dynamic arguments);

		// protected

		protected int ConvertDebuggerLineToClient(int line) => _clientLinesStartAt1 ? line : line - 1;
		protected int ConvertClientLineToDebugger(int line) => _clientLinesStartAt1 ? line : line + 1;

		protected string ConvertDebuggerPathToClient(string path)
		{
			if (_clientPathsAreURI) {
				try {
					var uri = new System.Uri(path);
					return uri.AbsoluteUri;
				}
				catch {
					return null;
				}
			}
			else {
				return path;
			}
		}

		protected string ConvertClientPathToDebugger(string clientPath)
		{
			if (clientPath == null) {
				return null;
			}

			if (_clientPathsAreURI) {
				if (Uri.IsWellFormedUriString(clientPath, UriKind.Absolute)) {
					Uri uri = new Uri(clientPath);
					return uri.LocalPath;
				}
				Program.Log($"path not well formed: '{clientPath}'");
				return null;
			}
			else {
				return clientPath;
			}
		}
	}
}
