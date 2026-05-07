using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Daystrom.ClaudeAgentSdk.Sessions;

/// <summary>
/// Adapter for mirroring session transcripts to external storage. Mirrors
/// <c>claude_agent_sdk.types.SessionStore</c>.
/// </summary>
/// <remarks>
/// <para>
/// The CLI subprocess always writes transcripts to local disk. When an
/// <see cref="ISessionStore"/> is supplied via
/// <see cref="ClaudeAgentOptions.SessionStore"/>, every batch of entries is
/// also passed to <see cref="AppendAsync"/>, and resume can materialise
/// from <see cref="LoadAsync"/> when the local file is absent.
/// </para>
/// <para>
/// Only <see cref="AppendAsync"/> and <see cref="LoadAsync"/> are required.
/// The remaining methods are optional and have default implementations
/// that throw <see cref="NotSupportedException"/> — implementers may
/// override only what their backend supports. Call sites probe for
/// behaviour at runtime rather than via type tests, matching the duck-typed
/// shape of Python's <c>Protocol</c>-based adapter.
/// </para>
/// <para>
/// Implementations are responsible for retention and concurrency control;
/// the SDK never deletes from the store unless <see cref="DeleteAsync"/> is
/// invoked explicitly.
/// </para>
/// </remarks>
public interface ISessionStore
{
    /// <summary>
    /// Mirror a batch of transcript entries. Called <i>after</i> the
    /// subprocess's local write succeeds; durability is already guaranteed
    /// locally. Most entries carry a stable <c>uuid</c> and should be
    /// upserted idempotently. Failures are surfaced as a
    /// <see cref="Messages.MirrorErrorMessage"/> after retries.
    /// </summary>
    Task AppendAsync(
        SessionKey key,
        IReadOnlyList<SessionStoreEntry> entries,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Load a full session for resume. Called once, in the SDK parent,
    /// before subprocess spawn; the result is materialised to a temporary
    /// JSONL file the subprocess resumes from. Return <c>null</c> for a
    /// key that was never written.
    /// </summary>
    Task<IReadOnlyList<SessionStoreEntry>?> LoadAsync(
        SessionKey key,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// List sessions for a project. Optional; backends that cannot list
    /// efficiently may leave the default implementation, which throws
    /// <see cref="NotSupportedException"/>.
    /// </summary>
    Task<IReadOnlyList<SessionStoreListEntry>> ListSessionsAsync(
        string projectKey,
        CancellationToken cancellationToken
    ) => throw new NotSupportedException();

    /// <summary>
    /// Return incrementally-maintained summaries for all sessions in a
    /// project. Optional; if unimplemented the SDK falls back to
    /// <see cref="ListSessionsAsync"/> + per-session
    /// <see cref="LoadAsync"/>.
    /// </summary>
    Task<IReadOnlyList<SessionSummaryEntry>> ListSessionSummariesAsync(
        string projectKey,
        CancellationToken cancellationToken
    ) => throw new NotSupportedException();

    /// <summary>
    /// Delete a session. Deleting a main-transcript key (no
    /// <see cref="SessionKey.Subpath"/>) must cascade to all subkeys.
    /// Optional; the default is a no-op (appropriate for WORM/append-only
    /// backends).
    /// </summary>
    Task DeleteAsync(SessionKey key, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// List all subpath keys under a session (e.g. sub-agent transcripts).
    /// Optional; if unimplemented, resume only materialises the main
    /// transcript.
    /// </summary>
    Task<IReadOnlyList<string>> ListSubkeysAsync(
        SessionListSubkeysKey key,
        CancellationToken cancellationToken
    ) => throw new NotSupportedException();
}
