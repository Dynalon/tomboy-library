using System;
using System.Collections.Generic;
using ServiceStack.ServiceClient.Web;
using Tomboy.Sync.DTO;
using DevDefined.OAuth.Framework;
using DevDefined.OAuth.Consumer;
using ServiceStack.Common;

namespace Tomboy.Sync.Web
{
	// proxy class, that provides connecticity to a remote web synchronization server like snowy/rainy/ubuntu1
	public class WebSyncServer : ISyncServer
	{
		private string mainServiceUrl;
		private string userServiceUrl;
		private string notesServiceUrl;

		private string oauthRequestTokenUrl;
		private string oauthAuthorizeUrl;
		private string oauthAccessTokenUrl;

		private IToken accessToken;

		public string ServerUrl;
		public WebSyncServer (string server_url, IToken access_token)
		{
			ServerUrl = server_url;
			mainServiceUrl = server_url + "/api/1.0";
			accessToken = access_token;
		}
		private void Connect ()
		{
			// with the first connection we find out the OAuth urls
			var restClient = new JsonServiceClient ("http://127.0.0.1:8080/johndoe/none/");
			restClient.SetAccessToken (accessToken);
			var api_response = restClient.Get<ApiResponse> ("/api/1.0/");

			// the server tells us the address of the user webservice
			this.userServiceUrl = api_response.UserRef.ApiRef;

			if (api_response.ApiVersion != "1.0") {
				throw new NotImplementedException ("unknown ApiVersion: " + api_response.ApiVersion);
			}

			this.oauthRequestTokenUrl = api_response.OAuthRequestTokenUrl;
			this.oauthAuthorizeUrl = api_response.OAuthAuthorizeUrl;
			this.oauthAccessTokenUrl = api_response.OAuthAccessTokenUrl;

			var user_response = restClient.Get<UserResponse> (this.userServiceUrl);
			this.notesServiceUrl = user_response.NotesRef.ApiRef;

			Console.WriteLine (this.notesServiceUrl);

			//this.LatestRevision = user_response.LatestSyncRevision;
			//this.Id = user_response.CurrentSyncGuid;

		}

		#region ISyncServer implementation

		public bool BeginSyncTransaction ()
		{
			Connect ();
			return true;
		}

		public bool CommitSyncTransaction ()
		{
			throw new NotImplementedException ();
		}

		public bool CancelSyncTransaction ()
		{
			throw new NotImplementedException ();
		}

		public IList<Note> GetAllNotes (bool include_note_content)
		{
			var restClient = new JsonServiceClient ();
			restClient.SetAccessToken (accessToken);

			var notes_response = restClient.Get<GetNotesResponse> (this.notesServiceUrl);

			IList<Note> notes = new List<Note> ();

			foreach (DTONote dto_note in notes_response.Notes) {
				var tomboy_note = new Note ();
				tomboy_note.PopulateWith (dto_note);

				notes.Add (tomboy_note);
			}

			return notes;
		}

		public IList<Note> GetNoteUpdatesSince (int revision)
		{
			throw new NotImplementedException ();
		}

		public void DeleteNotes (IList<string> deleteNotesGuids)
		{
			throw new NotImplementedException ();
		}

		public void UploadNotes (IList<Note> notes)
		{
			var restClient = new JsonServiceClient ();
			restClient.SetAccessToken (accessToken);

			var request = new PutNotesRequest ();
			request.LatestSyncRevision = 0;
			request.Notes = new List<DTONote> ();

			foreach (var tomboy_note in notes) {
				var dto_note = new DTONote ();
				dto_note.PopulateWith (tomboy_note);
				dto_note.LastSyncRevision = 0;
				dto_note.Tags = new string[] { };
				dto_note.Command = "update";
				request.Notes.Add (dto_note);

			}
			var repsonse = restClient.Put<PutNotesResponse> (notesServiceUrl, request);
//			var repsonse = restClient.Put<GetNotesResponse> ("http://127.0.0.1:8090/api/1.0/johndoe/notes", request);
		}

		public bool UpdatesAvailableSince (int revision)
		{
			throw new NotImplementedException ();
		}

		public IList<Note> DeletedServerNotes {
			get;
			// TODO remove set
			set;
		}

		public IList<Note> UploadedNotes {
			get;
			// TODO remove set
			set;
		}

		public int LatestRevision {
			get { return 0; }
			// TODO remove set
		}

		public string Id {
			get { return "0"; }
		}

		#endregion
	}

	public static class OAuthRestHelper
	{
		// helper extension method to sign each JSON request with OAuth
		public static void SetAccessToken (this JsonServiceClient client, IToken access_token)
		{
			// we use a request filter to add the required OAuth header
			client.LocalHttpWebRequestFilter += webservice_request => {
				
				OAuthConsumerContext consumer_context = new OAuthConsumerContext ();
				
				consumer_context.SignatureMethod = "HMAC-SHA1";
				consumer_context.ConsumerKey = access_token.ConsumerKey;
				consumer_context.ConsumerSecret = "anyone";
				consumer_context.UseHeaderForOAuthParameters = true;
				
				// the OAuth process creates a signature, which uses several data from
				// the web request like method, hostname, headers etc.
				OAuthContext request_context = new OAuthContext ();
				request_context.Headers = webservice_request.Headers;
				request_context.RequestMethod = webservice_request.Method;
				request_context.RawUri = webservice_request.RequestUri;
				
				// now create the signature for that context
				consumer_context.SignContextWithToken (request_context, access_token);
				
				// BUG TODO the oauth_token is not included when generating the header,
				// this is a bug ing DevDefined.OAuth. We add it manually as a workaround
				request_context.AuthorizationHeaderParameters.Add ("oauth_token", access_token.Token);
				
				string oauth_header = request_context.GenerateOAuthParametersForHeader ();
				
				webservice_request.Headers.Add ("Authorization", oauth_header);
				
			};
		}
	}
}

