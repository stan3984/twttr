import { useState } from "react";
import { PostForm } from "./PostForm";
import {
  describeCreatePostError,
  useCreatePost,
} from "./queries/createPostMutation";

export function PostComposer() {
  const createPost = useCreatePost();

  const [content, setContent] = useState("");

  return (
    <PostForm
      content={content}
      onContentChange={setContent}
      onSubmit={(normalized) =>
        createPost.mutate(
          { content: normalized },
          { onSuccess: () => setContent("") },
        )
      }
      isPending={createPost.isPending}
      submitLabel="Post"
      pendingLabel="Posting..."
      error={createPost.error ? describeCreatePostError(createPost.error) : null}
    />
  );
}
