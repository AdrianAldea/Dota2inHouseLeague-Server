// @ts-check
const eslint = require('@eslint/js');
const tseslint = require('typescript-eslint');
const angular = require('angular-eslint');

module.exports = tseslint.config(
  {
    files: ['**/*.ts'],
    extends: [
      eslint.configs.recommended,
      ...tseslint.configs.recommended,
      ...tseslint.configs.stylistic,
      ...angular.configs.tsRecommended,
    ],
    processor: angular.processInlineTemplates,
    rules: {
      // The codebase uses unprefixed selectors (e.g. `admin-page`, `card`).
      // Standardising on an `app-` prefix is tracked under Phase 6 of
      // REFACTORING_PLAN.md; leave these off for now so the gate stays green.
      '@angular-eslint/directive-selector': 'off',
      '@angular-eslint/component-selector': 'off',
      // The following surface real Phase 6 work (type the generic CardComponent,
      // migrate to inject()). Keep them as warnings so they're visible in lint
      // output without breaking the Phase 0 build gate.
      '@typescript-eslint/no-explicit-any': 'warn',
      '@typescript-eslint/no-inferrable-types': 'warn',
      '@angular-eslint/prefer-inject': 'warn',
    },
  },
  {
    files: ['**/*.html'],
    extends: [
      ...angular.configs.templateRecommended,
      ...angular.configs.templateAccessibility,
    ],
    rules: {
      // Accessibility cleanup (label associations, etc.) is tracked under
      // Phase 6 of REFACTORING_PLAN.md. Surface as warnings rather than
      // failing the build so the work is visible without blocking CI.
      '@angular-eslint/template/label-has-associated-control': 'warn',
    },
  },
);
