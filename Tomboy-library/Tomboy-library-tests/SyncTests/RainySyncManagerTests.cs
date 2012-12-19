using System;
using NUnit.Framework;
using DevDefined.OAuth.Framework;
using Rainy.Tests;
using Tomboy.Sync.Web;

namespace Tomboy.Sync
{

	[TestFixture]
	public class RainySyncManagerTests : Tomboy.Sync.AbstractSyncManagerTests
	{
		[SetUp]
		public void SetUp ()
		{
			RainyTestServer.BaseUri = "http://127.0.0.1:8080/johndoe/none";
			RainyTestServer.StartNewRainyStandaloneServer ();

			syncServer = new WebSyncServer ("http://127.0.0.1:8080/johndoe/none", RainyTestServer.GetAccessToken ());

		}

		[TearDown]
		public void TearDown ()
		{
			RainyTestServer.StopRainyStandaloneServer ();
		}

		protected override void ClearServer (bool reset = false)
		{
			return;
		}
	}
}
