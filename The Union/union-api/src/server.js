require('dotenv').config();
const express = require('express');
const cors = require('cors');

const membersRouter   = require('./routes/members');
const sessionsRouter  = require('./routes/sessions');
const passportRouter  = require('./routes/passport');
const bridgeRouter    = require('./routes/bridge');
const whatsappRouter  = require('./routes/whatsapp');

const app = express();
app.use(cors());
app.use(express.json());

app.use('/api/members',           membersRouter);
app.use('/api/sessions',          sessionsRouter);
app.use('/api/passport',          passportRouter);
app.use('/api/bridge',            bridgeRouter);           // ZKTeco push receiver
app.use('/api/bridge/whatsapp',   whatsappRouter);         // Meta WhatsApp inbound

app.get('/health', (_req, res) =>
  res.json({ status: 'ok', service: 'union-api', oasis: process.env.OASIS_API_BASE ?? 'https://api.oasisweb4.com/api' })
);

app.use((err, _req, res, _next) => {
  console.error(err.stack);
  res.status(500).json({ error: err.message });
});

const PORT = process.env.PORT || 3001;
app.listen(PORT, () => {
  console.log(`Union API running on :${PORT}`);
  console.log(`OASIS base: ${process.env.OASIS_API_BASE ?? 'https://api.oasisweb4.com/api'}`);

  if (process.env.ZKTECO_BRIDGE_ENABLED === 'true') {
    require('./jobs/syncAttendance');
  }
});
