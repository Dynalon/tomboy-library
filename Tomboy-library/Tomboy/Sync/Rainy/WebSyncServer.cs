using System;
using System.Collections.Generic;
using ServiceStack.ServiceClient.Web;
using Tomboy.Sync.DTO;
using DevDefined.OAuth.Framework;
using DevDefined.OAuth.Consumer;
using ServiceStack.Common;
using System.Linq;

namespace Tomboy.Sync.Web
{
	// proxy class, that provides connecticity to a remote web synchronization server like snowy/rainy/ubuntu1
	public class WebSyncServer : ISyncServer
	{
		private string rootApiUrl;
		private string userServiceUrl;
		private string notesServiceUrl;

		private string oauthRequestTokenUrl;
		private string oauthAuthorizeUrl;
		private string oauthAccessTokenUrl;

		private IToken accessToken;

		private string ServerUrl;

		// TODO access_Token must be better handled
		public WebSyncServer (string server_url, IToken access_token)
		{
			ServerUrl = server_url;
			rootApiUrl = server_url + "/api/1.0";
			accessToken = access_token;

			this.DeletedServerNotes = new List<string> ();
			this.UploadedNotes = new List<Note> ();
		}

		private JsonServiceClient GetJsonClient ()
		{
			var restClient = new JsonServiceClient ();
			restClient.SetAccessToken (accessToken);
			return restClient;
		}

		private void Connect ()
		{
			var restClient = GetJsonClient ();

			// with the first connection we find out the OAuth urls
			var api_response = restClient.Get<ApiResponse> (rootApiUrl);

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

			this.LatestRevision = user_response.LatestSyncRevision;
			this.Id = user_response.CurrentSyncGuid;

		}

		#region ISyncServer implementation

		public bool BeginSyncTransaction ()
		{
			Connect ();
			return true;
		}

		public bool CommitSyncTransaction ()
		{
			// TODO is this really correct? can we always assume the server advances
			// the current revision by only + 1 ?? Maybe better call the UserApi
			// to check the real revision?
			LatestRevision++;
			return true;
		}

		public bool CancelSyncTransaction ()
		{
			// TODO
			return true;
		}

		public IList<Note> GetAllNotes (bool include_note_content)
		{
			var restClient = GetJsonClient ();
			var response = restClient.Get<GetNotesResponse> (this.notesServiceUrl);

			return response.Notes.ToTomboyNotes ();
		}

		public IList<Note> GetNoteUpdatesSince (long revision)
		{
			var restClient = GetJsonClient ();

			// we have to add the ?since parameter to our uri
			var notes_request_url = notesServiceUrl + "?since=" + revision;

			var response = restClient.Get<GetNotesResponse> (notes_request_url);

			return response.Notes.ToTomboyNotes ();
		}

		public void DeleteNotes (IList<string> delete_note_guids)
		{
			var restClient = GetJsonClient ();

			// to delete notes, we call PutNotes and set the command to 'delete'
			var request = new PutNotesRequest ();

			request.Notes = new List<DTONote> ();
			foreach (string delete_guid in delete_note_guids) {
				request.Notes.Add (new DTONote () {
					Guid = delete_guid,
					Command = "delete"
				});
				DeletedServerNotes.Add (delete_guid);
			}

			restClient.Put<PutNotesRequest> (notesServiceUrl, request);
		}

		public void UploadNotes (IList<Note> notes)
		{
			var restClient = GetJsonClient ();

			var request = new PutNotesRequest ();
			//request.LatestSyncRevision = this.LatestRevision;
			request.Notes = notes.ToDTONotes ();

			restClient.Put<PutNotesResponse> (notesServiceUrl, request);

			// TODO if conflicts arise, this may be different
			UploadedNotes = notes;
		}

		public bool UpdatesAvailableSince (int revision)
		{
			throw new NotImplementedException ();
		}

		public IList<string> DeletedServerNotes {
			get; private set;
		}

		public IList<Note> UploadedNotes {
			get; private set;
		}

		public long LatestRevision {
			get ; private set;
		}

		public string Id {
			get; private set;
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
