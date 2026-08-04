import { useState, type SyntheticEvent } from "react";
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

interface Props {
  content: string;
  onContentChange: (content: string) => void;
  onSubmit: (normalized: string) => void;
  isPending: boolean;
  submitLabel: string;
  pendingLabel: string;
  error?: string | null;
  onCancel?: () => void;
}

export function PostForm({
  content,
  onContentChange,
  onSubmit,
  isPending,
  submitLabel,
  pendingLabel,
  error,
  onCancel,
}: Readonly<Props>) {
  const [invalid, setInvalid] = useState<string | null>(null);

  const normalized = normalize(content);

  function submit(event: SyntheticEvent) {
    event.preventDefault();

    const problem = validate(normalized);
    setInvalid(problem);
    if (problem) {
      return;
    }

    onSubmit(normalized);
  }

  const errorMessage = invalid ?? error;

  return (
    <form onSubmit={submit} className="flex flex-col gap-4">
      <textarea
        className="w-full px-3 py-2 border rounded border-slate-300"
        placeholder="What's happening?"
        rows={3}
        value={content}
        onChange={(e) => onContentChange(e.target.value)}
      />
      <p className="text-sm text-slate-600">
        {CONTENT_MAX - normalized.length} characters left
      </p>
      {errorMessage && <p className="text-sm text-red-600">{errorMessage}</p>}
      <div className="flex justify-center gap-2">
        <button
          type="submit"
          disabled={isPending}
          className="h-full px-4 py-2 text-white border rounded w-fit border-slate-900 bg-slate-900 disabled:opacity-50"
        >
          {isPending ? pendingLabel : submitLabel}
        </button>
        {onCancel && (
          <button
            type="button"
            onClick={onCancel}
            disabled={isPending}
            className="h-full px-4 py-2 border rounded w-fit border-slate-300 disabled:opacity-50"
          >
            Cancel
          </button>
        )}
      </div>
    </form>
  );
}
