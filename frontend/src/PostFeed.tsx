import { useMemo, useState } from "react";
import { usePosts } from "./queries/postsQuery";
import { useAuthors } from "./queries/userQuery";
import { describeApiError } from "./queries/util";
import type { TPost, TUser } from "./types";

const RELATIVE = new Intl.RelativeTimeFormat("en-UK", { numeric: "auto" });

const DIVISIONS = [
  ["second", 60, 0],
  ["minute", 60, 0],
  ["hour", 24, 1],
  ["day", 7, 1],
  ["week", 4.35, 1],
  ["month", 12, 1],
  ["year", Infinity, 1],
] as const;

// https://stackoverflow.com/questions/6108819/javascript-timestamp-to-relative-time
function formatTimeAgo(date: Date) {
  let delta = (date.getTime() - Date.now()) / 1000;

  for (const [unit, span, decimals] of DIVISIONS) {
    if (Math.abs(delta) < span) {
      const factor = Math.pow(10, decimals);
      return RELATIVE.format(Math.round(delta * factor) / factor, unit);
    }

    delta /= span;
  }

  throw new Error("unreachable");
}

interface Props {
  post: TPost;
  author?: TUser;
}

function PostItem({ post, author }: Readonly<Props>) {
  const lines = post.content.split(/\r\n|\r|\n/).length;
  const limit = 3;
  const isLong = lines > limit;
  const [isExpanded, setExpanded] = useState(false);

  return (
    <article className="p-4 border rounded border-slate-300">
      <p className="text-sm text-slate-600">
        {author
          ? `${author.displayName} @${author.username}`
          : "Unknown author"}
        {" · "}
        <time dateTime={post.createdAt}>
          {formatTimeAgo(new Date(post.createdAt))}
        </time>
      </p>
      <p
        className={`mt-2 whitespace-pre-wrap text-slate-900 ${isLong && !isExpanded ? "line-clamp-3" : ""}`}
      >
        {post.content}
      </p>
      {isLong && (
        <div className="flex justify-center">
          <button
            onClick={() => setExpanded((old) => !old)}
            className="hover:bg-blue-50 rounded text-xs px-1 py-0.5 cursor-pointer"
          >
            {isExpanded ? "Collapse content" : "Expand content"}
          </button>
        </div>
      )}
    </article>
  );
}

export function PostFeed() {
  const {
    data,
    error,
    isPending,
    isError,
    hasNextPage,
    isFetchingNextPage,
    fetchNextPage,
  } = usePosts();

  const posts = useMemo(
    () => [
      ...new Map(
        (data?.pages.flat() ?? []).map((post) => [post.id, post]),
      ).values(),
    ],
    [data?.pages],
  );

  const authors = useAuthors(posts);

  if (isPending) {
    return <p className="text-sm text-slate-600">Loading posts...</p>;
  }

  if (isError) {
    return <p className="text-sm text-red-600">{describeApiError(error)}</p>;
  }

  if (posts.length === 0) {
    return <p className="text-sm text-slate-600">No posts yet.</p>;
  }

  return (
    <div className="flex flex-col gap-4 mb-4 mt-8">
      {posts.map((post) => (
        <PostItem
          key={post.id}
          post={post}
          author={authors.get(post.authorId)}
        />
      ))}
      {hasNextPage && (
        <button
          onClick={() => fetchNextPage()}
          disabled={isFetchingNextPage}
          className="px-4 py-2 border rounded border-slate-300 disabled:opacity-50"
        >
          {isFetchingNextPage ? "Loading..." : "Load more"}
        </button>
      )}
    </div>
  );
}
