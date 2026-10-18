const apiOrigin = process.env.AFTERWORD_API_ORIGIN?.trim().replace(/\/+$/, "");

if (!apiOrigin) {
  throw new Error(
    "Set AFTERWORD_API_ORIGIN in Vercel to the public URL of the Afterword API service."
  );
}

const parsedApiOrigin = new URL(apiOrigin);
if (parsedApiOrigin.protocol !== "https:") {
  throw new Error("AFTERWORD_API_ORIGIN must use HTTPS.");
}

export const config = {
  buildCommand: "npm run build",
  outputDirectory: "wwwroot",
  cleanUrls: true,
  trailingSlash: false,
  rewrites: [
    {
      source: "/api/:path*",
      destination: `${parsedApiOrigin.origin}/api/:path*`,
    },
  ],
};
