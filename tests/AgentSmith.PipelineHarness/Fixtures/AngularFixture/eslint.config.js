const tseslint = require('typescript-eslint');

module.exports = tseslint.config(
  { ignores: ['dist/**', 'out-tsc/**', '.angular/**', 'node_modules/**'] },
  ...tseslint.configs.recommended,
);
