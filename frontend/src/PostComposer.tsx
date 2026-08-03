import { useState, type SyntheticEvent } from "react";
import {
  describeCreatePostError,
  useCreatePost,
} from "./queries/createPostMutation";
import { CONTENT_MAX, CONTENT_MIN } from "./types";

// The server normalizes line endings and Unicode before it validates, so do the same
// first -- otherwise a textarea's \r\n counts as two characters here and one there.
// String.normalize() matches .NET's Normalize().
function normalize(content: string) {
  return content.replace(/\r\n/g, "\n").normalize();
}

function validate(content: string) {
  if (content.length < CONTENT_MIN || content.length > CONTENT_MAX) {
    return `Post must be ${CONTENT_MIN}-${CONTENT_MAX} characters.`;
  }

  if (content !== content.trim()) {
    return "Post must not start or end with a space.";
  }

  return null;
}

export function PostComposer() {
  const createPost = useCreatePost();

  const [content, setContent] = useState("");
  const [invalid, setInvalid] = useState<string | null>(null);

  const normalized = normalize(content);

  function onSubmit(event: SyntheticEvent) {
    event.preventDefault();

    const problem = validate(normalized);
    setInvalid(problem);
    if (problem) {
      return;
    }

    createPost.mutate(
      { content: normalized },
      { onSuccess: () => setContent("") },
    );
  }

  const error =
    invalid ??
    (createPost.error ? describeCreatePostError(createPost.error) : null);

  return (
    <form onSubmit={onSubmit} className="flex flex-col gap-4">
      <textarea
        className="w-full px-3 py-2 border rounded border-slate-300"
        placeholder="What's happening?"
        rows={3}
        value={content}
        onChange={(e) => setContent(e.target.value)}
      />
      <p className="text-sm text-slate-600">
        {CONTENT_MAX - normalized.length} characters left
      </p>
      {error && <p className="text-sm text-red-600">{error}</p>}
      <button
        type="submit"
        disabled={createPost.isPending}
        className="w-full py-2 text-white rounded bg-slate-900 disabled:opacity-50"
      >
        {createPost.isPending ? "Posting..." : "Post"}
      </button>
    </form>
  );
}
