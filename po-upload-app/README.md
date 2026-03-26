# PO Upload App

React/Vite frontend for uploading purchase orders and viewing recent processing history.

## What It Does

- uploads PO documents to an n8n webhook
- shows processing status and the returned decision
- displays cached and refreshed upload history from a second n8n webhook

## Required Environment Variables

Copy the example file and provide org-owned endpoints:

```bash
cp .env.example .env
```

Required values:

- `VITE_N8N_WEBHOOK_URL`
- `VITE_N8N_HISTORY_URL`

If either value is missing, the UI now surfaces a configuration error instead of calling a baked-in default endpoint.

## Local Development

```bash
npm install
npm run dev
```

## Production Build

```bash
npm run build
npm run preview
```
