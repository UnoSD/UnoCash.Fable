const path = require("path");
const webpack = require("webpack");
const HtmlWebpackPlugin = require('html-webpack-plugin');
const MiniCssExtractPlugin = require("mini-css-extract-plugin");
const CopyWebpackPlugin = require('copy-webpack-plugin');

var babelOptions = {
    presets: [
        ["@babel/preset-env", {
            "targets": {
                "browsers": ["last 2 versions"]
            },
            "modules": false,
            "useBuiltIns": "usage",
            "corejs": 3,
            // This saves around 4KB in minified bundle (not gzipped)
            // "loose": true,
        }]
    ],
};

var commonPlugins = [
    new HtmlWebpackPlugin({
        filename: './index.html',
        template: './src/index.html'
    })
];

module.exports = (env, options) => {

    // If no mode has been defined, default to `development`
    if (options.mode === undefined)
        options.mode = "development";

    var isProduction = options.mode === "production";
    console.log("Bundling for " + (isProduction ? "production" : "development") + "...");

    return {
        devtool: 'inline-source-map',
        entry: isProduction ? // We don't use the same entry for dev and production, to make HMR over style quicker for dev env
            {
                demo: [
                    "@babel/polyfill",
                    './src/UnoCash.Fulma.fsproj',
                    './src/scss/main.scss'
                ]
            } : {
                app: [
                    "@babel/polyfill",
                    './src/UnoCash.Fulma.fsproj'
                ],
                style: [
                    './src/scss/main.scss'
                ]
            },
        output: {
            path: path.join(__dirname, './output'),
            filename: isProduction ? '[name].[hash].js' : '[name].js'
        },
        plugins: isProduction ?
            commonPlugins.concat([
                new MiniCssExtractPlugin({
                    filename: 'style.css'
                }),
                new CopyWebpackPlugin({
                    patterns: [
                    { from: './static' }
                ]})
            ])
            : commonPlugins.concat([
                new webpack.HotModuleReplacementPlugin(),
                new webpack.NamedModulesPlugin()
            ]),
        devServer: {
            contentBase: './static/',
            publicPath: "/",
            port: 8080,
            hot: true,
            inline: true,
            headers: {
                'Set-Cookie': 'jwtToken=eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJodHRwczovL2xvZ2luLm1pY3Jvc29mdG9ubGluZS5jb20vMDAwMDAwMDAtMDAwMC0wMDAwLTAwMDAtMDAwMDAwMDAwMDAwL3YyLjAiLCJpYXQiOjE2MTQ1ODk4MjksImV4cCI6MTY0NjEyNTgyOSwiYXVkIjoiMTExMTExMTEtMTExMS0xMTExLTExMTEtMTExMTExMTExMTExIiwic3ViIjoibXlfYWFkX3N1YmplY3QiLCJ1cG4iOiJ1bm9zZF9leHRlcm5hbG1haWwuY29tI0VYVCNAbXlkaXJlY3Rvcnkub25taWNyb3NvZnQuY29tIiwibmFtZSI6IlVub1NEIiwicHJlZmVycmVkX3VzZXJuYW1lIjoidW5vc2RAZXh0ZXJuYWxtYWlsLmNvbSJ9.eDEiXVVO8u2YCuqjWJ3id8cuWcmDcao1ix5y1ik5nlg'
            }
        },
        module: {
            rules: [
                {
                    test: /\.fs(x|proj)?$/,
                    use: {
                        loader: "fable-loader",
                        options: {
                            babel: babelOptions
                        }
                    }
                },
                {
                    test: /\.js$/,
                    exclude: /node_modules/,
                    use: {
                        loader: 'babel-loader',
                        options: babelOptions
                    },
                },
                {
                    test: /\.(sass|scss|css)$/,
                    use: [
                        isProduction
                            ? MiniCssExtractPlugin.loader
                            : 'style-loader',
                        'css-loader',
                        'sass-loader',
                    ],
                },
                {
                    test: /\.css$/,
                    use: ['style-loader', 'css-loader']
                },
                {
                    test: /\.(png|jpg|jpeg|gif|svg|woff|woff2|ttf|eot)(\?.*$|$)/,
                    use: ["file-loader"]
                }
            ]
        }
    };
}
