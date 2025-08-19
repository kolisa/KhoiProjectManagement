# Khoi Pro - Frontend

A modern React-based project management system frontend built with Tailwind CSS and Lucide React icons.

## Features

- **Authentication**: JWT-based login with role management
- **Responsive Design**: Mobile-first approach with Tailwind CSS
- **Real-time Updates**: Dynamic data loading and state management
- **Role-based UI**: Different interfaces for Admin, Manager, and Member roles
- **Modern UX**: Clean, professional interface with loading states and error handling

## Tech Stack

- **React 18**: Latest React with hooks and modern features
- **Tailwind CSS**: Utility-first CSS framework
- **Lucide React**: Beautiful, customizable icons
- **Modern JavaScript**: ES6+ features and async/await

## Getting Started

### Prerequisites

- Node.js 16+ and npm
- Running backend API (ASP.NET Core)

### Installation

1. **Clone and install dependencies:**
   ```bash
   git clone <repository-url>
   cd frontend
   npm install
   ```

2. **Configure environment:**
   ```bash
   cp .env.example .env
   # Edit .env with your API URL
   ```

3. **Start development server:**
   ```bash
   npm start
   ```

4. **Open browser:**
   Navigate to `http://localhost:3000`

### Build for Production

```bash
npm run build
```

## Project Structure

```
src/
├── App.js              # Main application component
├── index.js            # React entry point
├── index.css           # Global styles and Tailwind
└── components/         # (Future: Component organization)
    ├── Auth/
    ├── Dashboard/
    ├── Projects/
    ├── Tasks/
    └── Common/
```

## Key Components

### Authentication
- **AuthProvider**: Context provider for authentication state
- **AuthGuard**: Route protection component
- **LoginForm**: User authentication interface

### Main Features
- **Dashboard**: Overview with statistics and recent activity
- **Projects**: Project management with CRUD operations
- **Tasks**: Task management with status updates
- **Team**: Team member management
- **Reports**: Report generation and download

### Utility Components
- **StatusBadge**: Task status indicators
- **PriorityBadge**: Priority level indicators
- **RoleBadge**: User role indicators
- **TagsList**: Tag display component
- **LoadingSpinner**: Loading state indicator
- **ErrorMessage**: Error display with retry option

## API Integration

The app connects to the ASP.NET Core backend via:

- **ApiService**: Centralized API communication class
- **JWT Authentication**: Automatic token management
- **Error Handling**: Comprehensive error management
- **Request Interceptors**: Automatic authentication header injection

## Responsive Design

- **Mobile-first**: Optimized for mobile devices
- **Breakpoints**: Responsive across all screen sizes
- **Touch-friendly**: Mobile gesture support
- **Accessibility**: WCAG compliant design patterns

## Development

### Adding New Features

1. Create component in appropriate directory
2. Add API integration if needed
3. Update routing and navigation
4. Add proper error handling
5. Test across devices

### Styling Guidelines

- Use Tailwind utility classes
- Follow consistent spacing (4, 6, 8, 12, 16...)
- Use semantic color classes
- Maintain accessibility standards

### State Management

- **React Context**: Global state (auth, theme)
- **useState**: Local component state
- **useEffect**: Side effects and data loading
- **Custom Hooks**: Reusable stateful logic

## Deployment

### Netlify/Vercel

1. Build the project: `npm run build`
2. Deploy the `build` folder
3. Configure environment variables
4. Set up domain and SSL

### Traditional Hosting

1. Build: `npm run build`
2. Upload `build` folder contents
3. Configure web server for SPA routing
4. Set up HTTPS

## Environment Variables

```env
REACT_APP_API_URL=https://localhost:7001/api
REACT_APP_VERSION=1.0.0
REACT_APP_TITLE=Khoi Pro
```

## Browser Support

- Chrome (latest)
- Firefox (latest)
- Safari (latest)
- Edge (latest)
- Mobile browsers (iOS Safari, Chrome Mobile)

## Performance

- **Code Splitting**: Lazy loading for better performance
- **Optimized Images**: Proper image compression
- **Caching**: Efficient API response caching
- **Bundle Size**: Minimized production builds

## Testing

```bash
npm test        # Run tests
npm run test:coverage  # Coverage report
```

## Contributing

1. Follow existing code style
2. Add tests for new features
3. Update documentation
4. Submit pull request

## License

MIT License - see LICENSE file for details.