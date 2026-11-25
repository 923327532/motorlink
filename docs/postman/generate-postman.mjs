// Genera la colección Postman v2.1 a partir de openapi.json (Swagger de la API).
// Uso:  node generate-postman.mjs            (lee ./openapi.json y escribe ../MotorLink.postman_collection.json)
// Para actualizar openapi.json:  curl http://localhost:5080/swagger/v1/swagger.json -o openapi.json
import { readFileSync, writeFileSync } from 'node:fs';

const spec = JSON.parse(readFileSync(new URL('./openapi.json', import.meta.url), 'utf8'));
const schemas = spec.components?.schemas ?? {};

const resolve = (s) => (s?.$ref ? schemas[s.$ref.split('/').pop()] ?? {} : s ?? {});

// Ejemplo de cuerpo a partir del esquema (valores representativos, no datos reales).
function example(schema, depth = 0) {
  const s = resolve(schema);
  if (depth > 4) return null;
  if (s.example !== undefined) return s.example;
  if (s.enum) return s.enum[0];
  if (s.allOf) return Object.assign({}, ...s.allOf.map((x) => example(x, depth + 1)));
  if (s.oneOf || s.anyOf) return example((s.oneOf ?? s.anyOf).find((x) => x.type !== 'null') ?? {}, depth + 1);
  const type = Array.isArray(s.type) ? s.type.find((t) => t !== 'null') : s.type;
  switch (type) {
    case 'object': {
      const o = {};
      for (const [k, v] of Object.entries(s.properties ?? {})) o[k] = example(v, depth + 1);
      return o;
    }
    case 'array': return [example(s.items, depth + 1)];
    case 'integer': return s.minimum ?? 1;
    case 'number': return s.minimum ?? 10;
    case 'boolean': return false;
    case 'string':
      if (s.format === 'uuid') return '00000000-0000-0000-0000-000000000000';
      if (s.format === 'date-time') return new Date().toISOString();
      if (s.format === 'email') return 'usuario@ejemplo.com';
      return s.pattern ? 'VALOR' : 'texto';
    default: return null;
  }
}

const folders = new Map();
const pathVars = { id: '{{id}}', listingId: '{{listingId}}', vehicleId: '{{vehicleId}}', imageId: '{{imageId}}' };

for (const [path, methods] of Object.entries(spec.paths)) {
  for (const [method, op] of Object.entries(methods)) {
    if (!['get', 'post', 'put', 'patch', 'delete'].includes(method)) continue;
    const tag = op.tags?.[0] ?? 'General';
    if (!folders.has(tag)) folders.set(tag, []);

    const url = path.replace(/\{(\w+)\}/g, (_, n) => pathVars[n] ?? `{{${n}}}`);
    const query = (op.parameters ?? []).filter((p) => p.in === 'query')
      .map((p) => ({ key: p.name, value: '', disabled: true, description: resolve(p.schema).type ?? '' }));

    const headers = [];
    let body;
    const content = op.requestBody?.content ?? {};
    if (content['application/json']) {
      headers.push({ key: 'Content-Type', value: 'application/json' });
      body = { mode: 'raw', raw: JSON.stringify(example(content['application/json'].schema), null, 2), options: { raw: { language: 'json' } } };
    } else if (content['multipart/form-data']) {
      body = { mode: 'formdata', formdata: [{ key: 'file', type: 'file', src: [] }] };
    }

    const isPublic = !(op.security?.length);
    const item = {
      name: `${method.toUpperCase()} ${path.replace('/api/v1', '')}`,
      request: {
        method: method.toUpperCase(),
        header: headers,
        url: { raw: `{{baseUrl}}${url}`, host: ['{{baseUrl}}'], path: url.split('/').filter(Boolean), query },
        description: op.summary ?? op.description ?? '',
        body,
        auth: isPublic ? { type: 'noauth' } : undefined
      },
      response: []
    };

    // Guarda los tokens automáticamente al registrarse / iniciar sesión / renovar.
    if (['/api/v1/auth/login', '/api/v1/auth/register', '/api/v1/auth/refresh'].includes(path) && method === 'post') {
      item.event = [{
        listen: 'test',
        script: { type: 'text/javascript', exec: [
          'if (pm.response.code === 200 || pm.response.code === 201) {',
          '  const r = pm.response.json();',
          "  pm.collectionVariables.set('accessToken', r.accessToken);",
          "  pm.collectionVariables.set('refreshToken', r.refreshToken);",
          '}'
        ] }
      }];
    }
    folders.get(tag).push(item);
  }
}

const collection = {
  info: {
    name: 'MotorLink API v1',
    description: 'Generada desde el OpenAPI de la API. 1) Ejecuta POST /auth/login (o register): los tokens se guardan solos. ' +
      '2) El resto de solicitudes usa {{accessToken}} como Bearer. Los valores de ejemplo son representativos: reemplaza los identificadores ({{id}}, {{listingId}}, ...). ' +
      'Los enums se envían en MAYÚSCULAS (PENDING_REVIEW, WORK_RENT...).',
    schema: 'https://schema.getpostman.com/json/collection/v2.1.0/collection.json'
  },
  auth: { type: 'bearer', bearer: [{ key: 'token', value: '{{accessToken}}', type: 'string' }] },
  variable: [
    { key: 'baseUrl', value: 'http://localhost:5080' },
    { key: 'accessToken', value: '' }, { key: 'refreshToken', value: '' },
    { key: 'id', value: '' }, { key: 'listingId', value: '' }, { key: 'vehicleId', value: '' }, { key: 'imageId', value: '' }
  ],
  item: [...folders.entries()].sort(([a], [b]) => a.localeCompare(b)).map(([name, item]) => ({ name, item }))
};

const out = new URL('../MotorLink.postman_collection.json', import.meta.url);
writeFileSync(out, JSON.stringify(collection, null, 2));
const total = collection.item.reduce((n, f) => n + f.item.length, 0);
console.log(`Colección generada: ${collection.item.length} carpetas, ${total} solicitudes -> ${out.pathname}`);
