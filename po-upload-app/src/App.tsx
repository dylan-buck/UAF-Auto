import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { Navbar } from './components/Navbar';
import { UploadPage } from './pages/UploadPage';
import { LogsPage } from './pages/LogsPage';

function App() {
  return (
    <BrowserRouter>
      <div className="min-h-screen bg-white flex flex-col">
        <Navbar />
        <Routes>
          <Route path="/" element={<UploadPage />} />
          <Route path="/logs" element={<LogsPage />} />
        </Routes>
      </div>
    </BrowserRouter>
  );
}

export default App;
