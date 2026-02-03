import { NavLink } from 'react-router-dom';

export function Navbar() {
  return (
    <header className="border-b border-gray-100">
      <div className="max-w-4xl mx-auto px-6 py-4 flex items-center justify-between">
        <img src="/uaflogo.png" alt="United Air Filter" className="h-8" />

        <nav className="flex gap-1">
          <NavLink
            to="/"
            className={({ isActive }) =>
              `px-4 py-2 text-sm font-medium rounded-lg transition-colors ${
                isActive
                  ? 'bg-gray-100 text-gray-900'
                  : 'text-gray-500 hover:text-gray-700 hover:bg-gray-50'
              }`
            }
          >
            Upload
          </NavLink>
          <NavLink
            to="/logs"
            className={({ isActive }) =>
              `px-4 py-2 text-sm font-medium rounded-lg transition-colors ${
                isActive
                  ? 'bg-gray-100 text-gray-900'
                  : 'text-gray-500 hover:text-gray-700 hover:bg-gray-50'
              }`
            }
          >
            Logs
          </NavLink>
        </nav>
      </div>
    </header>
  );
}
