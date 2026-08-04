import { useMemo, useState } from "react";
import { PostForm } from "./PostForm";
import { useIdentity } from "./queries/identityQuery";
import { usePosts } from "./queries/postsQuery";
import {
  describeUpdatePostError,
  useUpdatePost,
} from "./queries/updatePostMutation";
import { useDeletePost } from "./queries/deletePostMutation";
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
      if (unit === "second") {
        return "a few seconds ago";
      }

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
  isAuthor: boolean;
}

function PostItem({ post, author, isAuthor }: Readonly<Props>) {
  const lines = post.content.split(/\r\n|\r|\n/).length;
  const limit = 3;
  const isLong = lines > limit;
  const [isExpanded, setExpanded] = useState(false);
  const [isEditing, setEditing] = useState(false);
  const [draft, setDraft] = useState(post.content);
  const updatePost = useUpdatePost(post.id);
  const deletePost = useDeletePost(post.id);
  const hasButtons = isAuthor || isLong;

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
        {post.updatedAt &&
          ` (edited ${formatTimeAgo(new Date(post.updatedAt))})`}
      </p>
      {isEditing ? (
        <div className="mt-2">
          <PostForm
            content={draft}
            onContentChange={setDraft}
            onSubmit={(normalized) =>
              updatePost.mutate(
                { content: normalized },
                { onSuccess: () => setEditing(false) },
              )
            }
            isPending={updatePost.isPending}
            submitLabel="Save"
            pendingLabel="Saving..."
            error={
              updatePost.error
                ? describeUpdatePostError(updatePost.error)
                : null
            }
            onCancel={() => setEditing(false)}
          />
        </div>
      ) : (
        <>
          <p
            className={`mt-2 whitespace-pre-wrap text-slate-900 ${isLong && !isExpanded ? "line-clamp-3" : ""}`}
          >
            {post.content}
          </p>
          {hasButtons && (
            <div className="flex gap-2 mt-2">
              {isLong && (
                <button
                  onClick={() => setExpanded((old) => !old)}
                  className="hover:bg-blue-50 rounded text-xs px-1 py-0.5 cursor-pointer"
                >
                  {isExpanded ? "Collapse content" : "Expand content"}
                </button>
              )}
              {isAuthor && (
                <>
                  <button
                    onClick={() => setEditing(true)}
                    className="hover:bg-blue-50 rounded text-xs px-1 py-0.5 cursor-pointer"
                  >
                    Edit
                  </button>
                  <button
                    onClick={() => {
                      if (confirm("Do you really want to delete this post?")) {
                        deletePost.mutate();
                      }
                    }}
                    disabled={deletePost.isPending}
                    className="hover:bg-red-50 rounded text-xs px-1 py-0.5 cursor-pointer disabled:cursor-not-allowed"
                  >
                    Delete
                  </button>
                </>
              )}
            </div>
          )}
        </>
      )}
    </article>
  );
}

export function PostFeed() {
  const { data: identity } = useIdentity();
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
    <div className="flex flex-col gap-4 mt-8 mb-4">
      {posts.map((post) => (
        <PostItem
          key={post.id}
          post={post}
          author={authors.get(post.authorId)}
          isAuthor={post.authorId === identity?.id}
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
