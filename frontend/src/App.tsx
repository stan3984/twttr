import { Route, Routes } from "react-router";
import { Home } from "./Home";
import RequireAuth from "./RequireAuth";
import { SignIn } from "./SignIn";
import { SignUp } from "./SignUp";

function App() {
  return (
    <Routes>
      <Route path="/signin" element={<SignIn />} />
      <Route path="/signup" element={<SignUp />} />
      <Route
        path="/"
        element={
          <RequireAuth>
            <Home />
          </RequireAuth>
        }
      />
    </Routes>
  );
}

export default App;
