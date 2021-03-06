using System;
using System.Linq;
using NUnit.Framework;
using Rainy.Db;
using Rainy.WebService;
using ServiceStack.ServiceClient.Web;
using Tomboy.Sync;
using Tomboy.Sync.Web;
using Tomboy.Sync.Web.DTO;

namespace Rainy.Tests.RestApi
{
	public abstract class NoteHistoryTestsBase : Tomboy.Sync.AbstractSyncManagerTestsBase
	{
		protected RainyTestServer testServer;
		protected DBUser testUser;

		protected string GetNoteHistoryUrl (string guid) {
			var url = new GetNoteHistoryRequest () {
				Guid = guid,
				Username = RainyTestServer.TEST_USER
			}.ToUrl ("GET");
			return testServer.ListenUrl + url;
		}
		protected string GetArchivedNoteUrl (string guid, long revision) {
			var url = new GetArchivedNoteRequest () {
				Guid = guid,
				Username = RainyTestServer.TEST_USER,
				Revision = revision
			}.ToUrl ("GET");
			return testServer.ListenUrl + url;
		}

		[Test]
		public void NoteHistoryIsEmpty ()
		{
			FirstSyncForBothSides ();
			var client = testServer.GetJsonClient ();

			var first_note = clientEngineOne.GetNotes ().Values.First ();
			var url = GetNoteHistoryUrl (first_note.Guid);
			var resp = client.Get<NoteHistoryResponse> (url);

			Assert.AreEqual (0, resp.CurrentRevision);
			//Assert.AreEqual (0, resp.Versions.Length);
		}



		[Test]
		[ExpectedException]
		public void ExceptionForUnknownNote ()
		{
			var client = testServer.GetJsonClient ();
			var url = GetNoteHistoryUrl (Guid.NewGuid ().ToString ());
			client.Get<NoteHistoryResponse> (url);

		}

		[Test]
		public void NoteHistoryIsPresentWithOneNoteAfterChange ()
		{
			FirstSyncForBothSides ();

			var first_note = clientEngineOne.GetNotes ().Values.First ();
			var new_title = "Some other title";
			var old_title = first_note.Title;
			first_note.Title = new_title;
			clientEngineOne.SaveNote (first_note);

			var sync_manager = new SyncManager (syncClientOne, syncServer);
			sync_manager.DoSync ();

			var client = testServer.GetJsonClient ();
			var url = GetNoteHistoryUrl (first_note.Guid);
			var resp = client.Get<NoteHistoryResponse> (url);

			Assert.AreEqual (1, resp.Versions.Length);
			Assert.AreEqual (1, resp.Versions[0].Revision);
			Assert.AreEqual (old_title, resp.Versions[0].Note.Title);

		}

		[Test]
		public void RetrieveAnArchivedVersionOfANote ()
		{
			FirstSyncForBothSides ();

			Tomboy.Note first_note = clientEngineOne.GetNotes ().Values.First ();
			DTONote first_note_dto = first_note.ToDTONote ();

			var new_title = "Some other title";
			var old_title = first_note_dto.Title;
			var new_text = "Some new text";
			var old_text = first_note_dto.Text;

			first_note.Title = new_title;
			first_note.Text = new_text;

			clientEngineOne.SaveNote (first_note);

			var sync_manager = new SyncManager (syncClientOne, syncServer);
			sync_manager.DoSync ();

			var client = testServer.GetJsonClient ();
			var url = GetNoteHistoryUrl (first_note.Guid);
			var resp = client.Get<NoteHistoryResponse> (url);

			var rev = resp.Versions[0].Revision;

			url = GetArchivedNoteUrl (first_note.Guid, rev);
			var note = client.Get<DTONote> (url);

			Assert.AreEqual (old_text, note.Text);
			Assert.AreEqual (old_title, note.Title);
			Assert.AreEqual (first_note_dto.Tags, note.Tags);

		}

		[Test]
		public void NoteArchiveDoesHonorTheIncludeTextParameter ()
		{
			FirstSyncForBothSides ();

			Tomboy.Note first_note = clientEngineOne.GetNotes ().Values.First ();
			first_note.Title = "different";
			clientEngineOne.SaveNote (first_note);

			var sync_manager = new SyncManager (syncClientOne, syncServer);
			sync_manager.DoSync ();

			var client = testServer.GetJsonClient ();
			var url = GetNoteHistoryUrl (first_note.Guid);
			var resp = client.Get<NoteHistoryResponse> (url + "?include_text=false");

			foreach (var archived_note in resp.Versions) {
				Assert.AreEqual ("", archived_note.Note.Text);
			}

			resp = client.Get<NoteHistoryResponse> (url + "?include_text=true");
			foreach (var archived_note in resp.Versions) {
				Assert.AreNotEqual ("", archived_note.Note.Text);
			}
		}

		[Test]
		public void NoteArchiveContainsAllGuids ()
		{
			FirstSyncForBothSides ();

			// now, lets delete a note from the client
			var first_note = clientEngineOne.GetNotes ().First ().Value;
			first_note.Title = "different";
			clientEngineOne.SaveNote (first_note);

			var sync_manager = new SyncManager (syncClientOne, syncServer);
			sync_manager.DoSync ();

			var client = testServer.GetJsonClient ();
			var url = testServer.ListenUrl + new GetNoteArchiveRequest (){ Username = RainyTestServer.TEST_USER }.ToUrl ("GET");
			var resp = client.Get<NoteArchiveResponse> (url);

			Assert.That (resp.Guids.Contains (first_note.Guid));
			Assert.AreEqual (1, resp.Guids.Count ());
		}

		[Test]
		public void NoteArchiveContainsNoteDataButNoText ()
		{
			FirstSyncForBothSides ();

			// now, lets delete a note from the client
			var first_note = clientEngineOne.GetNotes ().First ().Value;
			first_note.Title = "different";
			clientEngineOne.SaveNote (first_note);

			var sync_manager = new SyncManager (syncClientOne, syncServer);
			sync_manager.DoSync ();

			var client = testServer.GetJsonClient ();
			var url = testServer.ListenUrl + new GetNoteArchiveRequest (){ Username = RainyTestServer.TEST_USER }.ToUrl ("GET");
			var resp = client.Get<NoteArchiveResponse> (url);

			Assert.AreEqual ("", resp.Notes[0].Text);
		}

		[Test]
		public void DeletedNoteShowsUpInNoteArchive()
		{
			FirstSyncForBothSides ();

			// now, lets delete a note from the client
			var deleted_note = clientEngineOne.GetNotes ().First ().Value;
			clientEngineOne.DeleteNote (deleted_note);
			clientManifestOne.NoteDeletions.Add (deleted_note.Guid, deleted_note.Title);

			var sync_manager = new SyncManager (syncClientOne, syncServer);
			sync_manager.DoSync ();

			var client = testServer.GetJsonClient ();
			var url = testServer.ListenUrl + new GetNoteArchiveRequest (){ Username = RainyTestServer.TEST_USER }.ToUrl ("GET");
			var resp = client.Get<NoteArchiveResponse> (url);

			Assert.That (resp.Guids.Contains (deleted_note.Guid));
		}

		protected override void ClearServer (bool reset = false)
		{
			return;
		}
	}

	public class NoteHistoryTestsSqlite : NoteHistoryTestsBase
	{

		[SetUp]
		public new void SetUp ()
		{
			testServer = new RainyTestServer ();
			testServer.ScenarioSqlite ();
			testServer.Start ();

			syncServer = new WebSyncServer (testServer.BaseUri, testServer.GetAccessToken ());
		}

		[TearDown]
		public new void TearDown ()
		{
			testServer.Stop ();
		}
	}

	public class NoteHistoryTestsPostgres : NoteHistoryTestsBase
	{

		[SetUp]
		public new void SetUp ()
		{
			testServer = new RainyTestServer ();
			testServer.ScenarioPostgres ();
			testServer.Start ();

			syncServer = new WebSyncServer (testServer.BaseUri, testServer.GetAccessToken ());
		}

		[TearDown]
		public new void TearDown ()
		{
			testServer.Stop ();
		}
	}
	
}
