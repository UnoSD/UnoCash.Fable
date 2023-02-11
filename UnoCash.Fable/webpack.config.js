const path = require("path");
const HtmlWebpackPlugin = require('html-webpack-plugin');
const MiniCssExtractPlugin = require("mini-css-extract-plugin");
const CopyWebpackPlugin = require('copy-webpack-plugin');

const babelOptions = {
    presets: [
        ["@babel/preset-env", {
            "targets": {
                "browsers": ["last 2 versions"]
            },
            "modules": false,
            "useBuiltIns": "usage",
            "corejs": 3
        }]
    ],
};

const htmlWebpackPlugin =
    new HtmlWebpackPlugin({
        filename: './index.html',
        template: './index.html'
    });

module.exports = (env, options) => {
    
    // If no mode has been defined, default to `development`
    if (options.mode === undefined)
        options.mode = "development";

    const isProduction = options.mode === "production";

    return {
        optimization: {
            moduleIds: 'named',
            runtimeChunk: "single"
        },
        devtool: 'inline-source-map',
        entry: isProduction ? // We don't use the same entry for dev and production, to make HMR over style quicker for dev env
            {
                demo: [
                    "@babel/polyfill",
                    './App.fs.js',
                    './main.scss'
                ]
            } : {
                app: [
                    "@babel/polyfill",
                    './App.fs.js',
                ],
                style: [
                    './main.scss'
                ]
            },
        output: {
            path: path.join(__dirname, './output'),
            filename: isProduction ? '[name].[hash].js' : '[name].js'
        },
        plugins: isProduction ? [
                new MiniCssExtractPlugin({
                    filename: 'style.css'
                }),
                new CopyWebpackPlugin({
                    patterns: [
                        { from: './static' }
                    ]}),
                htmlWebpackPlugin
            ]
            : [ htmlWebpackPlugin ],
        devServer: {
            setupMiddlewares: (middlewares, devServer) => {
                devServer.app.get('/apibaseurl', (_, res) => {
                    res.send("http://localhost:7071");
                });
                
                return middlewares;
            },
            static: ['./static/'],
            devMiddleware: {
                publicPath: "/",
            },
            port: 8080,
            hot: true,
            headers: {
                // Anonymized example token
                'Set-Cookie': 'jwtToken=eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJodHRwczovL2xvZ2luLm1pY3Jvc29mdG9ubGluZS5jb20vMDAwMDAwMDAtMDAwMC0wMDAwLTAwMDAtMDAwMDAwMDAwMDAwL3YyLjAiLCJpYXQiOjE2MTQ1ODk4MjksImV4cCI6MTY0NjEyNTgyOSwiYXVkIjoiMTExMTExMTEtMTExMS0xMTExLTExMTEtMTExMTExMTExMTExIiwic3ViIjoibXlfYWFkX3N1YmplY3QiLCJ1cG4iOiJ1bm9zZF9leHRlcm5hbG1haWwuY29tI0VYVCNAbXlkaXJlY3Rvcnkub25taWNyb3NvZnQuY29tIiwibmFtZSI6IlVub1NEIiwicHJlZmVycmVkX3VzZXJuYW1lIjoidW5vc2RAZXh0ZXJuYWxtYWlsLmNvbSJ9.eDEiXVVO8u2YCuqjWJ3id8cuWcmDcao1ix5y1ik5nlg'
            }
        },
        module: {
            rules: [
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
                    test: /\.woff2?$/i,
                    type: 'asset/resource',
                    dependency: { not: ['url'] },
                },
                {
                    test: /\.ttf?$/i,
                    type: 'asset/resource',
                    dependency: { not: ['url'] },
                },
                {
                    test: /\.(png|jpg|jpeg|gif|svg|woff|eot)(\?.*$|$)/,
                    use: ["file-loader"]
                }
            ]
        }
    };
}
