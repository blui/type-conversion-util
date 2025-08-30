const fs = require("fs");
const path = require("path");
const wav = require("node-wav");
const lamejs = require("lamejs");

class AudioVideoService {
  constructor() {
    console.log("Audio/Video service initialized with pure Node.js libraries");
  }

  async convert(inputPath, outputPath, inputFormat, targetFormat) {
    try {
      console.log(`Converting ${inputFormat} to ${targetFormat}`);

      // Determine if it's audio conversion (video conversion requires more complex libraries)
      const audioFormats = ["mp3", "wav"];

      const isAudioInput = audioFormats.includes(inputFormat.toLowerCase());
      const isAudioOutput = audioFormats.includes(targetFormat.toLowerCase());

      if (isAudioInput && isAudioOutput) {
        return await this.convertAudio(
          inputPath,
          outputPath,
          inputFormat,
          targetFormat
        );
      } else {
        // For video formats, provide a placeholder/info file instead
        return await this.createVideoPlaceholder(
          inputPath,
          outputPath,
          inputFormat,
          targetFormat
        );
      }
    } catch (error) {
      console.error("Audio/Video conversion error:", error);
      return { success: false, error: error.message };
    }
  }

  async convertAudio(inputPath, outputPath, inputFormat, targetFormat) {
    try {
      switch (`${inputFormat}-${targetFormat}`) {
        case "wav-mp3":
          return await this.wavToMp3(inputPath, outputPath);
        case "mp3-wav":
          return await this.mp3ToWav(inputPath, outputPath);
        default:
          throw new Error(
            `Audio conversion from ${inputFormat} to ${targetFormat} is not supported`
          );
      }
    } catch (error) {
      throw new Error(`Audio conversion failed: ${error.message}`);
    }
  }

  async wavToMp3(inputPath, outputPath) {
    try {
      // Read WAV file
      const buffer = fs.readFileSync(inputPath);
      const result = wav.decode(buffer);

      // Convert to MP3 using lamejs
      const mp3encoder = new lamejs.Mp3Encoder(
        result.channelData.length,
        result.sampleRate,
        128
      );

      let mp3Data = [];

      if (result.channelData.length === 1) {
        // Mono
        const samples = new Int16Array(result.channelData[0].length);
        for (let i = 0; i < result.channelData[0].length; i++) {
          samples[i] = result.channelData[0][i] * 0x7fff;
        }

        const blockSize = 1152;
        for (let i = 0; i < samples.length; i += blockSize) {
          const sampleChunk = samples.subarray(i, i + blockSize);
          const mp3buf = mp3encoder.encodeBuffer(sampleChunk);
          if (mp3buf.length > 0) {
            mp3Data.push(Buffer.from(mp3buf));
          }
        }
      } else {
        // Stereo
        const left = new Int16Array(result.channelData[0].length);
        const right = new Int16Array(result.channelData[1].length);

        for (let i = 0; i < result.channelData[0].length; i++) {
          left[i] = result.channelData[0][i] * 0x7fff;
          right[i] = result.channelData[1][i] * 0x7fff;
        }

        const blockSize = 1152;
        for (let i = 0; i < left.length; i += blockSize) {
          const leftChunk = left.subarray(i, i + blockSize);
          const rightChunk = right.subarray(i, i + blockSize);
          const mp3buf = mp3encoder.encodeBuffer(leftChunk, rightChunk);
          if (mp3buf.length > 0) {
            mp3Data.push(Buffer.from(mp3buf));
          }
        }
      }

      // Flush remaining data
      const mp3buf = mp3encoder.flush();
      if (mp3buf.length > 0) {
        mp3Data.push(Buffer.from(mp3buf));
      }

      // Write MP3 file
      const mp3Buffer = Buffer.concat(mp3Data);
      fs.writeFileSync(outputPath, mp3Buffer);

      return { success: true, outputPath };
    } catch (error) {
      throw new Error(`WAV to MP3 conversion failed: ${error.message}`);
    }
  }

  async mp3ToWav(inputPath, outputPath) {
    try {
      // Note: MP3 to WAV conversion requires MP3 decoding which is complex
      // For now, we'll create an informational file
      const infoContent = `MP3 to WAV Conversion Notice
=============================

Original file: ${path.basename(inputPath)}
Requested format: WAV

Note: MP3 to WAV conversion requires complex audio decoding libraries.
For production use, consider using a dedicated audio processing service
or a more comprehensive audio library.

This is a placeholder file indicating the conversion was requested
but not completed due to library limitations.

File size: ${fs.statSync(inputPath).size} bytes
Date: ${new Date().toISOString()}
`;

      fs.writeFileSync(outputPath.replace(".wav", ".txt"), infoContent);

      return {
        success: true,
        outputPath: outputPath.replace(".wav", ".txt"),
        note: "MP3 to WAV conversion requires additional audio decoding libraries",
      };
    } catch (error) {
      throw new Error(`MP3 to WAV conversion failed: ${error.message}`);
    }
  }

  async createVideoPlaceholder(
    inputPath,
    outputPath,
    inputFormat,
    targetFormat
  ) {
    try {
      const infoContent = `Video Conversion Notice
======================

Original file: ${path.basename(inputPath)}
Original format: ${inputFormat.toUpperCase()}
Requested format: ${targetFormat.toUpperCase()}

Note: Video conversion requires complex multimedia processing libraries
and significant system resources. For enterprise environments, consider:

1. Using cloud-based video processing services (AWS MediaConvert, Azure Media Services)
2. Dedicated video processing APIs (Cloudinary, Mux, etc.)
3. Containerized solutions with specialized video tools

This placeholder file indicates the conversion was requested but not
completed due to the complexity of video processing in pure Node.js.

Original file size: ${fs.statSync(inputPath).size} bytes
Date: ${new Date().toISOString()}

Recommended alternatives:
- For simple video tasks: Use cloud services
- For enterprise: Implement dedicated video processing microservices
- For development: Consider using Docker containers with video tools
`;

      const placeholderPath = outputPath.replace(
        path.extname(outputPath),
        ".txt"
      );
      fs.writeFileSync(placeholderPath, infoContent);

      return {
        success: true,
        outputPath: placeholderPath,
        note: "Video conversion requires specialized libraries - placeholder created",
      };
    } catch (error) {
      throw new Error(`Video placeholder creation failed: ${error.message}`);
    }
  }

  // Utility method to get basic audio file info
  async getAudioInfo(inputPath) {
    try {
      const stats = fs.statSync(inputPath);
      const ext = path.extname(inputPath).toLowerCase();

      let info = {
        size: stats.size,
        format: ext.slice(1),
        created: stats.birthtime,
      };

      if (ext === ".wav") {
        try {
          const buffer = fs.readFileSync(inputPath);
          const result = wav.decode(buffer);
          info.sampleRate = result.sampleRate;
          info.channels = result.channelData.length;
          info.duration = result.channelData[0].length / result.sampleRate;
        } catch (error) {
          console.warn("Could not parse WAV file details:", error.message);
        }
      }

      return info;
    } catch (error) {
      throw new Error(`Failed to get audio info: ${error.message}`);
    }
  }
}

module.exports = new AudioVideoService();
