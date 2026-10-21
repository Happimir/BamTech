import type { CodegenConfig } from '@graphql-codegen/cli';

/**
 * Generates typed operations from the API's committed schema (src/StargateAPI/schema.graphql,
 * written by the API's `schema export`) and the .graphql documents beside the code that uses them.
 * Run `npm run codegen` after changing either; the output is committed.
 */
const config: CodegenConfig = {
  schema: '../src/StargateAPI/schema.graphql',
  documents: 'src/app/**/*.graphql',
  generates: {
    'src/app/core/data-access/graphql/generated.ts': {
      plugins: ['typescript-operations', 'typed-document-node'],
      config: {
        // IDs are opaque UUID strings and dates are YYYY-MM-DD calendar-date strings.
        scalars: { UUID: 'string', LocalDate: 'string' },
        enumsAsTypes: true,
        useTypeImports: true,
        avoidOptionals: { field: true, inputValue: false },
        nonOptionalTypename: false,
      },
    },
    // Lets Apollo's cache match fragments on the Error interface and the result unions.
    'src/app/core/data-access/graphql/possible-types.ts': {
      plugins: ['fragment-matcher'],
      config: { useExplicitTyping: true },
    },
  },
  hooks: { afterAllFileWrite: ['prettier --write'] },
};

export default config;
