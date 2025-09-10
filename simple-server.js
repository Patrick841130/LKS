const express = require('express');
const cors = require('cors');
const path = require('path');
const fs = require('fs');

const app = express();
const port = 3001;

// Define static directory
const staticDir = path.join(__dirname, 'src/LksBrothers.Explorer/wwwroot');

// Middleware
app.use(cors());
app.use(express.json());

// Serve hero.html as the main page (prioritize over index.html)
app.get('/', (req, res) => {
    const heroPath = path.join(staticDir, 'hero.html');
    
    // Check if hero.html exists
    if (fs.existsSync(heroPath)) {
        console.log('🌟 Serving hero.html as main LKS Network page');
        res.sendFile(heroPath);
    } else {
        console.log('❌ hero.html not found, serving 404');
        res.status(404).send(`
            <h1>File Not Found</h1>
            <p>hero.html not found in ${staticDir}</p>
            <p>Available files: ${fs.readdirSync(staticDir).join(', ')}</p>
        `);
    }
});

// Static files middleware (after custom routes)
app.use(express.static(staticDir));

// Serve explorer at /explorer route
app.get('/explorer', (req, res) => {
    const indexPath = path.join(staticDir, 'index.html');
    
    if (fs.existsSync(indexPath)) {
        console.log('🔍 Serving LKS Explorer at /explorer');
        res.sendFile(indexPath);
    } else {
        res.status(404).send('Explorer not found');
    }
});

// Health check endpoint
app.get('/health', (req, res) => {
    res.json({ 
        status: 'OK', 
        timestamp: new Date().toISOString(),
        service: 'LKS Network Development Server'
    });
});

// API endpoints for development
app.get('/api/status', (req, res) => {
    res.json({
        network: 'LKS Network',
        chainId: 1337,
        zeroFees: true,
        status: 'active'
    });
});

app.listen(port, () => {
    console.log(`🚀 LKS Network Development Server running on http://localhost:${port}`);
    console.log(`📁 Serving files from: src/LksBrothers.Explorer/wwwroot/`);
    console.log(`🌐 Open http://localhost:${port} to view your application`);
});
